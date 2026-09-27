using Application.Abstractions.Assets;
using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Materials;
using Application.Abstractions.Messaging;
using Application.Abstractions.Posting;
using Application.DocumentLines;
using Domain.Common;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.WarehouseDocuments.Submit;

internal sealed class SubmitDocumentCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IApplicationTransaction transaction,
    IMaterialOperationLock materialLock,
    IOptions<AssetCreationOptions> assetCreationOptions,
    IEnumerable<IDocumentSubmissionValidator> typeValidators)
    : ICommandHandler<SubmitDocumentCommand>
{
    public Task<Result> Handle(SubmitDocumentCommand command, CancellationToken cancellationToken) =>
        transaction.ExecuteAsync(
            ct => SubmitInTransactionAsync(command, ct),
            cancellationToken);

    private async Task<Result> SubmitInTransactionAsync(
        SubmitDocumentCommand command,
        CancellationToken cancellationToken)
    {
        WarehouseDocument? document = await context.WarehouseDocuments
            .SingleOrDefaultAsync(d => d.Id == command.DocumentId, cancellationToken);

        if (document is null)
        {
            return Result.Failure(WarehouseDocumentErrors.NotFound(command.DocumentId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.Submit,
            ScopeType.Warehouse,
            document.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure(WarehouseDocumentErrors.NotFound(command.DocumentId));
        }

        if (document.RowVersion != command.ExpectedRowVersion)
        {
            return Result.Failure(WarehouseDocumentErrors.RowVersionMismatch(
                command.DocumentId,
                command.ExpectedRowVersion,
                document.RowVersion));
        }

        // Submitting is what makes a document "operationally used" for the catalog, so it locks the
        // same material rows a classification edit locks. Without it, a catalog edit could read a
        // still-draft document, pass the operational-use guard, and commit a classification change
        // after this submit had already validated the lines against the older catalog revision.
        if (document.ReversalOfDocumentId is null)
        {
            List<Guid> materialIds = await context.DocumentLines
                .AsNoTracking()
                .Where(line => line.DocumentId == command.DocumentId)
                .Select(line => line.MaterialId)
                .Distinct()
                .ToListAsync(cancellationToken);

            await materialLock.AcquireAsync(materialIds, cancellationToken);
        }

        Result lineValidationResult = await DocumentLineSubmissionValidator.ValidateAsync(
            context,
            document,
            assetCreationOptions.Value,
            typeValidators,
            cancellationToken);

        if (lineValidationResult.IsFailure)
        {
            return lineValidationResult;
        }

        Result submitResult = document.Submit();

        if (submitResult.IsFailure)
        {
            return submitResult;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            int? currentRowVersion = await context.WarehouseDocuments
                .AsNoTracking()
                .Where(d => d.Id == command.DocumentId)
                .Select(d => (int?)d.RowVersion)
                .SingleOrDefaultAsync(cancellationToken);

            return Result.Failure(WarehouseDocumentErrors.RowVersionMismatch(
                command.DocumentId,
                command.ExpectedRowVersion,
                currentRowVersion));
        }

        return Result.Success();
    }
}

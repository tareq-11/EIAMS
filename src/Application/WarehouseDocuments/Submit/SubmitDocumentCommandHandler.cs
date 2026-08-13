using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.DocumentLines;
using Domain.Common;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.WarehouseDocuments.Submit;

internal sealed class SubmitDocumentCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<SubmitDocumentCommand>
{
    public async Task<Result> Handle(SubmitDocumentCommand command, CancellationToken cancellationToken)
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

        Result lineValidationResult = await DocumentLineSubmissionValidator.ValidateAsync(
            context,
            document,
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

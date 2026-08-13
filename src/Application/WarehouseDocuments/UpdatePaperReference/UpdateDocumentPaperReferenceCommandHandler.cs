using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.WarehouseDocuments.UpdatePaperReference;

internal sealed class UpdateDocumentPaperReferenceCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<UpdateDocumentPaperReferenceCommand>
{
    public async Task<Result> Handle(
        UpdateDocumentPaperReferenceCommand command,
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
            PermissionCodes.WarehouseDocuments.Edit,
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

        Result updateResult = document.UpdatePaperReference(command.PaperDocumentNumber, command.PaperDocumentYear);

        if (updateResult.IsFailure)
        {
            return updateResult;
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

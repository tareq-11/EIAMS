using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.DocumentLineAssetSelections;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.DocumentLineAssetSelections.Remove;

internal sealed class RemoveDocumentLineAssetSelectionCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<RemoveDocumentLineAssetSelectionCommand>
{
    public async Task<Result> Handle(
        RemoveDocumentLineAssetSelectionCommand command,
        CancellationToken cancellationToken)
    {
        WarehouseDocument? document = await context.WarehouseDocuments
            .SingleOrDefaultAsync(item => item.Id == command.DocumentId, cancellationToken);

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
            return Result.Failure(WarehouseDocumentErrors.NotFound(document.Id));
        }

        if (document.RowVersion != command.ExpectedRowVersion)
        {
            return Result.Failure(WarehouseDocumentErrors.RowVersionMismatch(
                document.Id,
                command.ExpectedRowVersion,
                document.RowVersion));
        }

        if (document.DocumentStatus != DocumentStatus.Draft)
        {
            return Result.Failure(WarehouseDocumentErrors.NotEditable(document.Id, document.DocumentStatus));
        }

        if (document.ReversalOfDocumentId is not null)
        {
            return Result.Failure(WarehouseDocumentErrors.ReversalLinesImmutable(document.Id));
        }

        DocumentLineAssetSelection? selection = await context.DocumentLineAssetSelections
            .SingleOrDefaultAsync(item => item.DocumentId == command.DocumentId &&
                item.DocumentLineId == command.LineId &&
                item.AssetId == command.AssetId, cancellationToken);

        if (selection is null)
        {
            return Result.Failure(DocumentLineAssetSelectionErrors.NotFound(command.LineId, command.AssetId));
        }

        selection.RaiseRemovedEvent();
        context.DocumentLineAssetSelections.Remove(selection);
        Result mutationResult = document.RegisterDetailMutation();

        if (mutationResult.IsFailure)
        {
            return mutationResult;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            int? current = await context.WarehouseDocuments.AsNoTracking()
                .Where(item => item.Id == command.DocumentId)
                .Select(item => (int?)item.RowVersion)
                .SingleOrDefaultAsync(cancellationToken);

            return Result.Failure(WarehouseDocumentErrors.RowVersionMismatch(
                command.DocumentId,
                command.ExpectedRowVersion,
                current));
        }

        return Result.Success();
    }
}

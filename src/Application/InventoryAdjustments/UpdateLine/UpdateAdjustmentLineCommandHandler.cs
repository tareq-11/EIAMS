using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.DocumentLines;
using Domain.Common;
using Domain.DocumentLines;
using Domain.InventoryAdjustments;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.InventoryAdjustments.UpdateLine;

internal sealed class UpdateAdjustmentLineCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<UpdateAdjustmentLineCommand>
{
    public async Task<Result> Handle(UpdateAdjustmentLineCommand command, CancellationToken cancellationToken)
    {
        WarehouseDocument? document = await context.WarehouseDocuments.SingleOrDefaultAsync(
            item => item.Id == command.DocumentId, cancellationToken);
        if (document is null || !await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId, PermissionCodes.WarehouseDocuments.Edit,
            ScopeType.Warehouse, document.WarehouseId, cancellationToken))
        {
            return Result.Failure(WarehouseDocumentErrors.NotFound(command.DocumentId));
        }

        Result guard = await AdjustmentLineMutationGuard.ValidateAsync(
            context, document, command.ExpectedRowVersion, cancellationToken);
        if (guard.IsFailure)
        {
            return guard;
        }

        DocumentLine? line = await context.DocumentLines.SingleOrDefaultAsync(
            item => item.Id == command.LineId && item.DocumentId == document.Id, cancellationToken);
        AdjustmentLine? adjustmentLine = await context.AdjustmentLines.SingleOrDefaultAsync(
            item => item.Id == command.LineId && item.AdjustmentId == document.Id, cancellationToken);
        if (line is null || adjustmentLine is null)
        {
            return Result.Failure(AdjustmentLineErrors.NotFound(command.LineId));
        }

        Result<DocumentLineCatalogContext> catalogResult = await DocumentLineCatalogResolver.ResolveAsync(
            context, line.MaterialId, command.UnitId, cancellationToken);
        if (catalogResult.IsFailure)
        {
            return Result.Failure(catalogResult.Error);
        }

        Result<decimal> baseQuantity = BaseQuantityCalculator.Calculate(
            line.MaterialId, Math.Abs(command.Difference), command.UnitId,
            catalogResult.Value.Family.BaseUnitId, catalogResult.Value.Conversion);
        if (baseQuantity.IsFailure)
        {
            return Result.Failure(baseQuantity.Error);
        }

        decimal signedBaseDifference = Math.Sign(command.Difference) * baseQuantity.Value;
        Result lineUpdate = line.Update(DocumentLineType.Normal, Math.Abs(command.Difference),
            command.UnitId, baseQuantity.Value, null, null, null, null);
        Result detailUpdate = adjustmentLine.Update(signedBaseDifference, command.Reason);
        if (lineUpdate.IsFailure || detailUpdate.IsFailure)
        {
            return lineUpdate.IsFailure ? lineUpdate : detailUpdate;
        }

        Result mutation = document.RegisterDetailMutation();
        if (mutation.IsFailure)
        {
            return mutation;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(await AdjustmentLineMutationGuard.RowVersionErrorAsync(
                context, document.Id, command.ExpectedRowVersion, cancellationToken));
        }

        return Result.Success();
    }
}

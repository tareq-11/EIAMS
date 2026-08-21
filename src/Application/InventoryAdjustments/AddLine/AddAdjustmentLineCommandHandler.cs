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

namespace Application.InventoryAdjustments.AddLine;

internal sealed class AddAdjustmentLineCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<AddAdjustmentLineCommand, Guid>
{
    public async Task<Result<Guid>> Handle(AddAdjustmentLineCommand command, CancellationToken cancellationToken)
    {
        WarehouseDocument? document = await context.WarehouseDocuments.SingleOrDefaultAsync(
            item => item.Id == command.DocumentId, cancellationToken);
        if (document is null || !await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId, PermissionCodes.WarehouseDocuments.Edit,
            ScopeType.Warehouse, document.WarehouseId, cancellationToken))
        {
            return Result.Failure<Guid>(WarehouseDocumentErrors.NotFound(command.DocumentId));
        }

        Result guard = await AdjustmentLineMutationGuard.ValidateAsync(
            context, document, command.ExpectedRowVersion, cancellationToken);
        if (guard.IsFailure)
        {
            return Result.Failure<Guid>(guard.Error);
        }

        bool duplicate = await context.DocumentLines.AsNoTracking().AnyAsync(
            item => item.DocumentId == document.Id && item.MaterialId == command.MaterialId,
            cancellationToken);
        if (duplicate)
        {
            return Result.Failure<Guid>(AdjustmentLineErrors.Duplicate(document.Id, command.MaterialId));
        }

        Result<DocumentLineCatalogContext> catalogResult = await DocumentLineCatalogResolver.ResolveAsync(
            context, command.MaterialId, command.UnitId, cancellationToken);
        if (catalogResult.IsFailure)
        {
            return Result.Failure<Guid>(catalogResult.Error);
        }

        DocumentLineCatalogContext catalog = catalogResult.Value;
        if (catalog.Material.IsAssetTracked)
        {
            return Result.Failure<Guid>(AdjustmentLineErrors.AssetQuantityAdjustmentNotSupported);
        }

        Result<decimal> baseQuantity = BaseQuantityCalculator.Calculate(
            command.MaterialId, Math.Abs(command.Difference), command.UnitId,
            catalog.Family.BaseUnitId, catalog.Conversion);
        if (baseQuantity.IsFailure)
        {
            return Result.Failure<Guid>(baseQuantity.Error);
        }

        decimal signedBaseDifference = Math.Sign(command.Difference) * baseQuantity.Value;
        var lineId = Guid.NewGuid();
        Result<DocumentLine> line = DocumentLine.Create(
            lineId, document.Id, command.MaterialId, DocumentLineType.Normal,
            Math.Abs(command.Difference), command.UnitId, baseQuantity.Value,
            null, null, null);
        Result<AdjustmentLine> adjustmentLine = AdjustmentLine.Create(
            lineId, document.Id, signedBaseDifference, command.Reason);
        if (line.IsFailure || adjustmentLine.IsFailure)
        {
            return Result.Failure<Guid>(line.IsFailure ? line.Error : adjustmentLine.Error);
        }

        context.DocumentLines.Add(line.Value);
        context.AdjustmentLines.Add(adjustmentLine.Value);
        Result mutation = document.RegisterDetailMutation();
        if (mutation.IsFailure)
        {
            return Result.Failure<Guid>(mutation.Error);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<Guid>(await AdjustmentLineMutationGuard.RowVersionErrorAsync(
                context, document.Id, command.ExpectedRowVersion, cancellationToken));
        }

        return lineId;
    }
}

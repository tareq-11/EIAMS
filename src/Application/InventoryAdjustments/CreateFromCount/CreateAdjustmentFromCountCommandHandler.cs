using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.WarehouseDocuments;
using Domain.Common;
using Domain.DocumentLines;
using Domain.InventoryAdjustments;
using Domain.InventoryCounts;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.InventoryAdjustments.CreateFromCount;

internal sealed class CreateAdjustmentFromCountCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IWarehouseDocumentDraftFactory draftFactory,
    IDatabaseExceptionClassifier databaseExceptionClassifier)
    : ICommandHandler<CreateAdjustmentFromCountCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateAdjustmentFromCountCommand command, CancellationToken cancellationToken)
    {
        InventoryCount? count = await context.InventoryCounts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == command.CountId, cancellationToken);
        if (count is null)
        {
            return Result.Failure<Guid>(InventoryCountErrors.NotFound(command.CountId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId, PermissionCodes.WarehouseDocuments.Create,
            ScopeType.Warehouse, count.WarehouseId, cancellationToken);
        if (count.Status != InventoryCountStatus.Closed || !authorized)
        {
            return Result.Failure<Guid>(InventoryCountErrors.NotFound(command.CountId));
        }

        if (await context.InventoryAdjustments.AnyAsync(item => item.CountId == count.Id, cancellationToken))
        {
            return Result.Failure<Guid>(InventoryAdjustmentErrors.AlreadyExistsForCount(count.Id));
        }

        var variances = await (
                from countLine in context.InventoryCountLines.AsNoTracking()
                join material in context.Materials.AsNoTracking() on countLine.MaterialId equals material.Id
                join family in context.MaterialFamilies.AsNoTracking() on material.FamilyId equals family.Id
                where countLine.CountId == count.Id && countLine.Difference != null && countLine.Difference != 0
                select new { CountLine = countLine, material.MaterialKind, material.RequiresAssetNumber, family.BaseUnitId })
            .ToListAsync(cancellationToken);

        if (variances.Count == 0 || variances.All(item =>
                item.MaterialKind == Domain.Materials.MaterialKind.Asset || item.RequiresAssetNumber))
        {
            return Result.Failure<Guid>(InventoryCountErrors.SnapshotEmpty(count.Id));
        }

        Result<WarehouseDocument> document = await draftFactory.CreateAsync(
            count.WarehouseId, DocumentType.Adjustment, cancellationToken);
        if (document.IsFailure)
        {
            return Result.Failure<Guid>(document.Error);
        }

        Result<InventoryAdjustment> adjustment = InventoryAdjustment.Create(
            document.Value.Id, count.Id, AdjustmentKind.Quantity, "Inventory count variance");
        context.WarehouseDocuments.Add(document.Value);
        context.InventoryAdjustments.Add(adjustment.Value);

        foreach (var variance in variances.Where(item =>
                     item.MaterialKind != Domain.Materials.MaterialKind.Asset && !item.RequiresAssetNumber))
        {
            decimal difference = variance.CountLine.Difference!.Value;
            var lineId = Guid.NewGuid();
            Result<DocumentLine> line = DocumentLine.Create(lineId, document.Value.Id,
                variance.CountLine.MaterialId, DocumentLineType.Normal, Math.Abs(difference),
                variance.BaseUnitId, Math.Abs(difference), null, null, null);
            Result<AdjustmentLine> adjustmentLine = AdjustmentLine.Create(lineId, document.Value.Id,
                difference, variance.CountLine.VarianceReason!);
            if (line.IsFailure || adjustmentLine.IsFailure)
            {
                return Result.Failure<Guid>(line.IsFailure ? line.Error : adjustmentLine.Error);
            }

            context.DocumentLines.Add(line.Value);
            context.AdjustmentLines.Add(adjustmentLine.Value);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (databaseExceptionClassifier.IsUniqueConstraintViolation(exception))
        {
            return Result.Failure<Guid>(InventoryAdjustmentErrors.AlreadyExistsForCount(count.Id));
        }

        return document.Value.Id;
    }
}

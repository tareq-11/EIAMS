using Application.Abstractions.Data;
using Application.Abstractions.InventoryCounts;
using Domain.Common;
using Domain.InventoryCounts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.InventoryCounts;

internal sealed class InventoryFreezePolicyService(IApplicationDbContext context)
    : IInventoryFreezePolicyService
{
    public async Task<InventoryFreezeEvaluation> EvaluateProvisionalAsync(
        IReadOnlyCollection<Guid> warehouseIds,
        CancellationToken cancellationToken)
    {
        Guid[] distinctWarehouseIds = warehouseIds.Distinct().OrderBy(id => id).ToArray();

        List<ActiveInventoryFreeze> activeCounts = await GetActiveCountsAsync(
            distinctWarehouseIds, cancellationToken);

        return new InventoryFreezeEvaluation(
            activeCounts,
            BuildWarnings(activeCounts, isProvisional: true),
            BlockingError: null,
            IsProvisional: true);
    }

    public async Task<InventoryFreezeEvaluation> EvaluateExactAsync(
        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> affectedMaterialsByWarehouse,
        CancellationToken cancellationToken)
    {
        Guid[] distinctWarehouseIds = affectedMaterialsByWarehouse.Keys.Distinct().OrderBy(id => id).ToArray();
        List<ActiveInventoryFreeze> activeCounts = await GetActiveCountsAsync(
            distinctWarehouseIds, cancellationToken);

        ActiveInventoryFreeze? blockingCount = null;
        foreach (Guid warehouseId in distinctWarehouseIds)
        {
            Guid[] materialIds = affectedMaterialsByWarehouse[warehouseId].Distinct().OrderBy(id => id).ToArray();
            if (materialIds.Length == 0)
            {
                continue;
            }

            blockingCount = await (
                from line in context.InventoryCountLines.AsNoTracking()
                join count in context.InventoryCounts.AsNoTracking() on line.CountId equals count.Id
                where count.WarehouseId == warehouseId &&
                      count.Status == InventoryCountStatus.InProgress &&
                      count.FreezePolicy == FreezePolicy.HardFreeze &&
                      materialIds.Contains(line.MaterialId)
                orderby count.Id
                select new ActiveInventoryFreeze(count.Id, count.WarehouseId, count.FreezePolicy))
                .FirstOrDefaultAsync(cancellationToken);

            if (blockingCount is not null)
            {
                break;
            }
        }

        return new InventoryFreezeEvaluation(
            activeCounts,
            BuildWarnings(activeCounts, isProvisional: false),
            blockingCount is null
                ? null
                : InventoryCountErrors.PostingBlocked(blockingCount.CountId, blockingCount.WarehouseId),
            IsProvisional: false);
    }

    private async Task<List<ActiveInventoryFreeze>> GetActiveCountsAsync(
        Guid[] distinctWarehouseIds,
        CancellationToken cancellationToken)
    {
        if (distinctWarehouseIds.Length == 0)
        {
            return [];
        }

        List<ActiveInventoryFreeze> activeCounts = await context.InventoryCounts
            .AsNoTracking()
            .Where(count =>
                distinctWarehouseIds.Contains(count.WarehouseId) &&
                count.Status == InventoryCountStatus.InProgress)
            .OrderBy(count => count.WarehouseId)
            .ThenBy(count => count.Id)
            .Select(count => new ActiveInventoryFreeze(
                count.Id,
                count.WarehouseId,
                count.FreezePolicy))
            .ToListAsync(cancellationToken);

        return activeCounts;
    }

    private static List<InventoryFreezeWarning> BuildWarnings(
        IReadOnlyCollection<ActiveInventoryFreeze> activeCounts,
        bool isProvisional)
    {
        var warnings = activeCounts
            .Where(count => count.FreezePolicy == FreezePolicy.SoftFreeze)
            .Select(count => new InventoryFreezeWarning(
                "InventoryCounts.SoftFreezeActive",
                "Posting continued while a soft-freeze inventory count is active.",
                count.CountId,
                count.WarehouseId))
            .ToList();

        if (isProvisional)
        {
            warnings.AddRange(activeCounts
                .Where(count => count.FreezePolicy == FreezePolicy.HardFreeze)
                .Select(count => new InventoryFreezeWarning(
                    "InventoryCounts.HardFreezeProvisional",
                    "A hard-freeze count is active in this warehouse; posting is blocked only if its exact material membership overlaps the operation.",
                    count.CountId,
                    count.WarehouseId)));
        }

        return warnings;
    }
}

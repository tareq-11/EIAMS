using Application.Abstractions.Data;
using Application.Abstractions.InventoryCounts;
using Domain.Common;
using Domain.InventoryCounts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.InventoryCounts;

internal sealed class InventoryFreezePolicyService(IApplicationDbContext context)
    : IInventoryFreezePolicyService
{
    public async Task<InventoryFreezeEvaluation> EvaluateAsync(
        IReadOnlyCollection<Guid> warehouseIds,
        CancellationToken cancellationToken)
    {
        Guid[] distinctWarehouseIds = warehouseIds.Distinct().OrderBy(id => id).ToArray();

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

        ActiveInventoryFreeze? blockingCount = activeCounts
            .FirstOrDefault(count => count.FreezePolicy == FreezePolicy.HardFreeze);

        var warnings = activeCounts
            .Where(count => count.FreezePolicy == FreezePolicy.SoftFreeze)
            .Select(count => new InventoryFreezeWarning(
                "InventoryCounts.SoftFreezeActive",
                "Posting continued while a soft-freeze inventory count is active.",
                count.CountId,
                count.WarehouseId))
            .ToList();

        return new InventoryFreezeEvaluation(
            activeCounts,
            warnings,
            blockingCount is null
                ? null
                : InventoryCountErrors.PostingBlocked(blockingCount.CountId, blockingCount.WarehouseId));
    }
}

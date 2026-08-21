using Domain.Common;
using SharedKernel;

namespace Application.Abstractions.InventoryCounts;

public interface IInventoryFreezePolicyService
{
    Task<InventoryFreezeEvaluation> EvaluateAsync(
        IReadOnlyCollection<Guid> warehouseIds,
        CancellationToken cancellationToken);
}

public sealed record ActiveInventoryFreeze(
    Guid CountId,
    Guid WarehouseId,
    FreezePolicy FreezePolicy);

public sealed record InventoryFreezeWarning(
    string Code,
    string Message,
    Guid CountId,
    Guid WarehouseId);

public sealed record InventoryFreezeEvaluation(
    IReadOnlyList<ActiveInventoryFreeze> ActiveCounts,
    IReadOnlyList<InventoryFreezeWarning> Warnings,
    Error? BlockingError);

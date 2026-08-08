using Domain.Common;
using SharedKernel;

namespace Application.Abstractions.Warehouses;

/// <summary>
/// Evaluates whether a warehouse is allowed to perform an operation for a material domain
/// (D-CAP-01) - derived at query time from Warehouse/MaterialDomain/WarehouseCapability/
/// WarehouseCapabilityOperation, never a stored flag. Consumed by every posting flow from M4
/// onward; not exposed over HTTP.
/// </summary>
public interface ICapabilityCheckService
{
    Task<Result> EnsureAllowedAsync(
        Guid warehouseId,
        Guid materialDomainId,
        OperationType operationType,
        CancellationToken cancellationToken);
}

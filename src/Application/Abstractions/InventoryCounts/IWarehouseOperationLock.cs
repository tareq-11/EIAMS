namespace Application.Abstractions.InventoryCounts;

public interface IWarehouseOperationLock
{
    Task AcquireAsync(IEnumerable<Guid> warehouseIds, CancellationToken cancellationToken);
}

namespace Application.Abstractions.Ledger;

/// <summary>Serializes mutations for warehouse/material inventory keys inside the current transaction.</summary>
public interface IInventoryKeyLock
{
    Task AcquireAsync(
        IEnumerable<(Guid WarehouseId, Guid MaterialId)> keys,
        CancellationToken cancellationToken);
}

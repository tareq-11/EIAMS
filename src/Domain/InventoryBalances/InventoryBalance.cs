using SharedKernel;

namespace Domain.InventoryBalances;

/// <summary>
/// The current on-hand quantity for one (Warehouse, Material) pair - an explainable cache, always
/// equal to the sum of that pair's StockMovement deltas (Ch. 4, D-INV-01). There is no public
/// management API: only the ledger posting/reversal writer (Infrastructure's
/// <c>IInventoryLedgerWriter</c>) ever creates or updates a row, always inside the posting
/// transaction after locking it.
/// </summary>
public sealed class InventoryBalance : Entity, IAuditableEntity
{
    private InventoryBalance() { }

    public Guid WarehouseId { get; private set; }
    public Guid MaterialId { get; private set; }
    public decimal Quantity { get; private set; }
    public DateTime LastUpdatedUtc { get; private set; }
    public int RowVersion { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static InventoryBalance CreateZero(Guid id, Guid warehouseId, Guid materialId, DateTime nowUtc)
    {
        var balance = new InventoryBalance
        {
            Id = id,
            WarehouseId = warehouseId,
            MaterialId = materialId,
            Quantity = 0,
            LastUpdatedUtc = nowUtc,
            RowVersion = 1
        };

        balance.Raise(new InventoryBalanceCreatedDomainEvent(balance.Id, warehouseId, materialId));

        return balance;
    }

    public Result SetQuantity(decimal quantity, DateTime nowUtc)
    {
        if (quantity < 0)
        {
            return Result.Failure(InventoryBalanceErrors.NegativeQuantity(WarehouseId, MaterialId, quantity));
        }

        Quantity = quantity;
        LastUpdatedUtc = nowUtc;
        RowVersion++;

        Raise(new InventoryBalanceUpdatedDomainEvent(Id, WarehouseId, MaterialId, quantity));

        return Result.Success();
    }
}

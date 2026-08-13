namespace Application.InventoryBalances.GetByWarehouse;

public sealed class InventoryBalanceResponse
{
    public Guid Id { get; init; }
    public Guid WarehouseId { get; init; }
    public Guid MaterialId { get; init; }
    public string MaterialCode { get; init; }
    public string MaterialNameAr { get; init; }
    public decimal Quantity { get; init; }
    public DateTime LastUpdatedUtc { get; init; }
    public int RowVersion { get; init; }
}

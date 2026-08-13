namespace Application.StockMovements.GetByDocument;

public sealed class StockMovementResponse
{
    public Guid Id { get; init; }
    public Guid WarehouseId { get; init; }
    public Guid MaterialId { get; init; }
    public Guid DocumentId { get; init; }
    public Guid LineId { get; init; }
    public string MovementType { get; init; }
    public decimal QuantityDelta { get; init; }
    public DateTime PostedAtUtc { get; init; }
    public Guid PostedBy { get; init; }
}

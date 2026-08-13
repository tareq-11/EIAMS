using Domain.Common;
using SharedKernel;

namespace Domain.StockMovements;

public sealed record StockMovementPostedDomainEvent(
    Guid MovementId,
    Guid WarehouseId,
    Guid MaterialId,
    MovementType MovementType,
    decimal QuantityDelta) : IDomainEvent;

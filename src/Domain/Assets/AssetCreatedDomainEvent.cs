using SharedKernel;

namespace Domain.Assets;

public sealed record AssetCreatedDomainEvent(
    Guid AssetId,
    Guid MaterialId,
    Guid WarehouseId,
    Guid ReceiptLineId) : IDomainEvent;

using Domain.Common;
using SharedKernel;

namespace Domain.AssetMovementHistories;

public sealed record AssetMovementHistoryAppendedDomainEvent(
    Guid HistoryId,
    Guid AssetId,
    Guid DocumentId,
    AssetMovementType MovementType) : IDomainEvent;

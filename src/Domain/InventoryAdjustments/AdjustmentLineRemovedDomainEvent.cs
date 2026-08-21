using SharedKernel;

namespace Domain.InventoryAdjustments;

public sealed record AdjustmentLineRemovedDomainEvent(Guid LineId, Guid AdjustmentId) : IDomainEvent;

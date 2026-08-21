using SharedKernel;

namespace Domain.InventoryAdjustments;

public sealed record AdjustmentLineUpdatedDomainEvent(Guid LineId, Guid AdjustmentId) : IDomainEvent;

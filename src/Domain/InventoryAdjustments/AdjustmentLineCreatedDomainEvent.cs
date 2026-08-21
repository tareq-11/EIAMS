using SharedKernel;

namespace Domain.InventoryAdjustments;

public sealed record AdjustmentLineCreatedDomainEvent(Guid LineId, Guid AdjustmentId) : IDomainEvent;

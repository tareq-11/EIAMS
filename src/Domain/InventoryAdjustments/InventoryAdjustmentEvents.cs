using Domain.Common;
using SharedKernel;

namespace Domain.InventoryAdjustments;

public sealed record InventoryAdjustmentCreatedDomainEvent(Guid AdjustmentId, Guid? CountId, AdjustmentKind Kind) : IDomainEvent;
public sealed record InventoryAdjustmentPostedDomainEvent(Guid AdjustmentId) : IDomainEvent;
public sealed record InventoryAdjustmentReversedDomainEvent(Guid AdjustmentId) : IDomainEvent;

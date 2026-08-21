using SharedKernel;

namespace Domain.InventoryCounts;

public sealed record InventoryCountPlannedDomainEvent(Guid CountId, Guid WarehouseId) : IDomainEvent;
public sealed record InventoryCountStartedDomainEvent(Guid CountId, Guid WarehouseId) : IDomainEvent;
public sealed record InventoryCountCompletedDomainEvent(Guid CountId) : IDomainEvent;
public sealed record InventoryCountClosedDomainEvent(Guid CountId) : IDomainEvent;
public sealed record InventoryCountActualRecordedDomainEvent(Guid CountId, Guid LineId) : IDomainEvent;
public sealed record InventoryCountVarianceReasonUpdatedDomainEvent(Guid CountId, Guid LineId) : IDomainEvent;

using SharedKernel;

namespace Domain.Warehouses;

public sealed record WarehouseUpdatedDomainEvent(Guid WarehouseId) : IDomainEvent;

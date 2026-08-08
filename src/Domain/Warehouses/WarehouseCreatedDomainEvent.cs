using SharedKernel;

namespace Domain.Warehouses;

public sealed record WarehouseCreatedDomainEvent(Guid WarehouseId, Guid SiteId) : IDomainEvent;

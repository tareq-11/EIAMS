using SharedKernel;

namespace Domain.Warehouses;

public sealed record WarehouseAdministrativeOwnerChangedDomainEvent(
    Guid WarehouseId,
    Guid? PreviousOrganizationalUnitId,
    Guid OrganizationalUnitId) : IDomainEvent;

using SharedKernel;

namespace Domain.OrganizationalUnits;

public sealed record OrganizationalUnitCreatedDomainEvent(Guid OrganizationalUnitId, Guid SiteId) : IDomainEvent;

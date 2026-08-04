using SharedKernel;

namespace Domain.OrganizationalUnits;

public sealed record OrganizationalUnitUpdatedDomainEvent(Guid OrganizationalUnitId) : IDomainEvent;

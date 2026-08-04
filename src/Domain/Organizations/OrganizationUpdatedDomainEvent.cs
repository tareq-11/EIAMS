using SharedKernel;

namespace Domain.Organizations;

public sealed record OrganizationUpdatedDomainEvent(Guid OrganizationId) : IDomainEvent;

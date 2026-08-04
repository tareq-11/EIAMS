using SharedKernel;

namespace Domain.Organizations;

public sealed record OrganizationCreatedDomainEvent(Guid OrganizationId) : IDomainEvent;

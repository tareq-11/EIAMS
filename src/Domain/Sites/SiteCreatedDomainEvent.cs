using SharedKernel;

namespace Domain.Sites;

public sealed record SiteCreatedDomainEvent(Guid SiteId, Guid OrganizationId) : IDomainEvent;

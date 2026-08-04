using SharedKernel;

namespace Domain.Sites;

public sealed record SiteUpdatedDomainEvent(Guid SiteId) : IDomainEvent;

using Domain.Common;
using SharedKernel;

namespace Domain.Sites;

public sealed record SiteStatusChangedDomainEvent(Guid SiteId, Status Status) : IDomainEvent;

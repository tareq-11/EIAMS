using Domain.Common;
using SharedKernel;

namespace Domain.Organizations;

public sealed record OrganizationStatusChangedDomainEvent(Guid OrganizationId, Status Status) : IDomainEvent;

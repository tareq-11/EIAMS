using Domain.Common;
using SharedKernel;

namespace Domain.OrganizationalUnits;

public sealed record OrganizationalUnitStatusChangedDomainEvent(Guid OrganizationalUnitId, Status Status) : IDomainEvent;

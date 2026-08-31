using Domain.Common;
using SharedKernel;

namespace Domain.ExternalParties;

public sealed record ExternalPartyCreatedDomainEvent(Guid ExternalPartyId) : IDomainEvent;
public sealed record ExternalPartyUpdatedDomainEvent(Guid ExternalPartyId) : IDomainEvent;
public sealed record ExternalPartyStatusChangedDomainEvent(Guid ExternalPartyId, Status Status) : IDomainEvent;

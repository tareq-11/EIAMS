using Domain.Common;
using SharedKernel;

namespace Domain.IssueTos;

public sealed record IssueToUpdatedDomainEvent(
    Guid DocumentId,
    PartyType RecipientType,
    Guid RecipientId) : IDomainEvent;

using Domain.Common;
using SharedKernel;

namespace Domain.IssueTos;

public sealed record IssueToCreatedDomainEvent(
    Guid DocumentId,
    PartyType RecipientType,
    Guid RecipientId) : IDomainEvent;

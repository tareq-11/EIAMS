using SharedKernel;

namespace Domain.DocumentLines;

public sealed record DocumentLineRemovedDomainEvent(Guid LineId, Guid DocumentId) : IDomainEvent;

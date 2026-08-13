using SharedKernel;

namespace Domain.DocumentLines;

public sealed record DocumentLineUpdatedDomainEvent(Guid LineId, Guid DocumentId) : IDomainEvent;

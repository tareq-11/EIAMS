using SharedKernel;

namespace Domain.DocumentLines;

public sealed record DocumentLineAddedDomainEvent(Guid LineId, Guid DocumentId, Guid MaterialId) : IDomainEvent;

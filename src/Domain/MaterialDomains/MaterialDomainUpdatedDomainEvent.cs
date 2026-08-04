using SharedKernel;

namespace Domain.MaterialDomains;

public sealed record MaterialDomainUpdatedDomainEvent(Guid MaterialDomainId) : IDomainEvent;

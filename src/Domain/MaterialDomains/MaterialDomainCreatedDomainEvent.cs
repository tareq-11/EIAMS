using SharedKernel;

namespace Domain.MaterialDomains;

public sealed record MaterialDomainCreatedDomainEvent(Guid MaterialDomainId) : IDomainEvent;

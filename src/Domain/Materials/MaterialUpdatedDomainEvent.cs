using SharedKernel;

namespace Domain.Materials;

public sealed record MaterialUpdatedDomainEvent(Guid MaterialId) : IDomainEvent;

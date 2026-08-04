using SharedKernel;

namespace Domain.MaterialFamilies;

public sealed record MaterialFamilyCreatedDomainEvent(Guid MaterialFamilyId, Guid CategoryId) : IDomainEvent;

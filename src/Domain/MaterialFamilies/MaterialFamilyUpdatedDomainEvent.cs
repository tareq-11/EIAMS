using SharedKernel;

namespace Domain.MaterialFamilies;

public sealed record MaterialFamilyUpdatedDomainEvent(Guid MaterialFamilyId) : IDomainEvent;

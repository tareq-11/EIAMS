using SharedKernel;

namespace Domain.Materials;

public sealed record MaterialCreatedDomainEvent(Guid MaterialId, Guid FamilyId) : IDomainEvent;

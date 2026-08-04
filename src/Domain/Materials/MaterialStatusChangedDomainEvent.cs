using SharedKernel;

namespace Domain.Materials;

public sealed record MaterialStatusChangedDomainEvent(Guid MaterialId, MaterialStatus Status) : IDomainEvent;

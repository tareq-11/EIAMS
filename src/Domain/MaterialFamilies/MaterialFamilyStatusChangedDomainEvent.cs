using Domain.Common;
using SharedKernel;

namespace Domain.MaterialFamilies;

public sealed record MaterialFamilyStatusChangedDomainEvent(Guid MaterialFamilyId, Status Status) : IDomainEvent;

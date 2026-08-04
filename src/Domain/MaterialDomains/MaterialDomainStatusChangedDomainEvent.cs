using Domain.Common;
using SharedKernel;

namespace Domain.MaterialDomains;

public sealed record MaterialDomainStatusChangedDomainEvent(Guid MaterialDomainId, Status Status) : IDomainEvent;

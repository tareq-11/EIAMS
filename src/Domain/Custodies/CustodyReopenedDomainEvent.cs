using SharedKernel;

namespace Domain.Custodies;

public sealed record CustodyReopenedDomainEvent(Guid CustodyId, Guid AssetId) : IDomainEvent;

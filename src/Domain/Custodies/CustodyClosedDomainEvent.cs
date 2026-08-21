using SharedKernel;

namespace Domain.Custodies;

public sealed record CustodyClosedDomainEvent(Guid CustodyId, Guid AssetId, Guid? ReturnDocumentId) : IDomainEvent;

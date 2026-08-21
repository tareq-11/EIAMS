using Domain.Common;
using SharedKernel;

namespace Domain.Custodies;

public sealed record CustodyOpenedDomainEvent(
    Guid CustodyId,
    Guid AssetId,
    PartyType HolderType,
    Guid HolderId,
    CustodyKind CustodyKind) : IDomainEvent;

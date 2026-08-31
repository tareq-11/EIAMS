using Domain.Common;
using SharedKernel;

namespace Domain.TrackedMaterialUnits;

public sealed record TrackedMaterialUnitIssuedDomainEvent(
    Guid UnitId,
    Guid MaterialId,
    string SerialNumber,
    PartyType HolderType,
    Guid HolderId,
    CustodyKind CustodyKind,
    Guid IssueDocumentId) : IDomainEvent;

public sealed record TrackedMaterialUnitReturnedDomainEvent(
    Guid UnitId,
    Guid MaterialId,
    Guid ReturnDocumentId,
    DateTime ReturnedAtUtc) : IDomainEvent;

public sealed record TrackedMaterialUnitTransferredDomainEvent(
    Guid UnitId,
    PartyType NewHolderType,
    Guid NewHolderId,
    CustodyKind NewCustodyKind,
    DateTime TransferredAtUtc) : IDomainEvent;

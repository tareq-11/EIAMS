using Domain.Common;
using SharedKernel;

namespace Domain.DurableCustodyAllocations;

public sealed record DurableCustodyAllocationOpenedDomainEvent(
    Guid AllocationId,
    Guid MaterialId,
    PartyType HolderType,
    Guid HolderId,
    CustodyKind CustodyKind,
    decimal IssuedQuantity,
    Guid IssueDocumentId) : IDomainEvent;

public sealed record DurableCustodyAllocationReturnedDomainEvent(
    Guid AllocationId,
    Guid MaterialId,
    decimal ReturnedQuantity,
    decimal RemainingActiveQuantity,
    Guid ReturnDocumentId,
    DateTime ReturnedAtUtc) : IDomainEvent;

public sealed record DurableCustodyAllocationTransferredDomainEvent(
    Guid AllocationId,
    PartyType NewHolderType,
    Guid NewHolderId,
    CustodyKind NewCustodyKind,
    DateTime TransferredAtUtc) : IDomainEvent;

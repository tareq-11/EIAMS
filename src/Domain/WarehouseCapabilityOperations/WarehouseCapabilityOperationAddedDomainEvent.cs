using Domain.Common;
using SharedKernel;

namespace Domain.WarehouseCapabilityOperations;

public sealed record WarehouseCapabilityOperationAddedDomainEvent(
    Guid CapabilityOperationId,
    Guid CapabilityId,
    OperationType OperationType) : IDomainEvent;

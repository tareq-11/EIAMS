using Domain.Common;
using SharedKernel;

namespace Domain.WarehouseCapabilityOperations;

public sealed record WarehouseCapabilityOperationRemovedDomainEvent(
    Guid CapabilityOperationId,
    Guid CapabilityId,
    OperationType OperationType) : IDomainEvent;

using Application.Abstractions.Messaging;

namespace Application.WarehouseCapabilityOperations.GetByCapability;

public sealed record GetWarehouseCapabilityOperationsQuery(Guid CapabilityId)
    : IQuery<List<WarehouseCapabilityOperationResponse>>;

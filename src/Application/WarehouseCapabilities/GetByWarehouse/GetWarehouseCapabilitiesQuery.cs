using Application.Abstractions.Messaging;

namespace Application.WarehouseCapabilities.GetByWarehouse;

public sealed record GetWarehouseCapabilitiesQuery(Guid WarehouseId) : IQuery<List<WarehouseCapabilityResponse>>;

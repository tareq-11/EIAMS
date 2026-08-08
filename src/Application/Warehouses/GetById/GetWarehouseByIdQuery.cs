using Application.Abstractions.Messaging;

namespace Application.Warehouses.GetById;

public sealed record GetWarehouseByIdQuery(Guid WarehouseId) : IQuery<WarehouseResponse>;

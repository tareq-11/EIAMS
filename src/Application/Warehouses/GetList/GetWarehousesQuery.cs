using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.Warehouses.GetList;

public sealed record GetWarehousesQuery(Guid? SiteId, Status? Status) : IQuery<List<WarehouseResponse>>;

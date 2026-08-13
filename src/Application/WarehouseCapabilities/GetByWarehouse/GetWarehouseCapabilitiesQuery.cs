using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.WarehouseCapabilities.GetByWarehouse;

public sealed record GetWarehouseCapabilitiesQuery(Guid WarehouseId, int Page, int PageSize)
    : IQuery<PagedResult<WarehouseCapabilityResponse>>;

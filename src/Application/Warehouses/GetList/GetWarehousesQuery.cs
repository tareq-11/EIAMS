using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;

namespace Application.Warehouses.GetList;

public sealed record GetWarehousesQuery(Guid? SiteId, Status? Status, int Page, int PageSize)
    : IQuery<PagedResult<WarehouseResponse>>;

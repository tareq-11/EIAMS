using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.Reports.Inventory;

public sealed record GetInventoryReportQuery(
    Guid? WarehouseId,
    Guid? MaterialId,
    string? Search,
    int Page,
    int PageSize) : IQuery<PagedResult<InventoryReportRow>>;

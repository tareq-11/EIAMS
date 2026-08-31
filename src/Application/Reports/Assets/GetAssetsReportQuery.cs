using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;

namespace Application.Reports.Assets;

public sealed record GetAssetsReportQuery(
    Guid? WarehouseId,
    AssetCurrentStatus? Status,
    int Page,
    int PageSize) : IQuery<PagedResult<AssetsReportRow>>;

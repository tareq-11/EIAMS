using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;

namespace Application.Assets.GetList;

public sealed record GetAssetsQuery(
    Guid? WarehouseId,
    Guid? MaterialId,
    AssetCurrentStatus? Status,
    string? Search,
    int Page,
    int PageSize) : IQuery<PagedResult<AssetResponse>>;

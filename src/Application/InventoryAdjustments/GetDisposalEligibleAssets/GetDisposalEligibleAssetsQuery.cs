using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.InventoryAdjustments.GetDisposalEligibleAssets;

public sealed record GetDisposalEligibleAssetsQuery(
    Guid? WarehouseId,
    Guid? MaterialId,
    string? Search,
    int Page,
    int PageSize) : IQuery<PagedResult<DisposalEligibleAssetResponse>>;

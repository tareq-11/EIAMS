using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.Assets.GetMovements;

public sealed record GetAssetMovementsQuery(Guid AssetId, int Page, int PageSize)
    : IQuery<PagedResult<AssetMovementResponse>>;

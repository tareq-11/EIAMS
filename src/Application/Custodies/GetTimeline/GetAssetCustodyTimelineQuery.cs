using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.Custodies.GetTimeline;

public sealed record GetAssetCustodyTimelineQuery(Guid AssetId, int Page, int PageSize)
    : IQuery<PagedResult<AssetCustodyTimelineResponse>>;

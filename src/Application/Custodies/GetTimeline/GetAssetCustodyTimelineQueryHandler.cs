using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Assets;
using Domain.Common;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Custodies.GetTimeline;

internal sealed class GetAssetCustodyTimelineQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetAssetCustodyTimelineQuery, PagedResult<AssetCustodyTimelineResponse>>
{
    public async Task<Result<PagedResult<AssetCustodyTimelineResponse>>> Handle(
        GetAssetCustodyTimelineQuery query,
        CancellationToken cancellationToken)
    {
        Guid? warehouseId = await context.Assets.AsNoTracking()
            .Where(item => item.Id == query.AssetId)
            .Select(item => item.WarehouseId)
            .SingleOrDefaultAsync(cancellationToken);

        if (warehouseId is null)
        {
            return Result.Failure<PagedResult<AssetCustodyTimelineResponse>>(
                AssetErrors.NotFound(query.AssetId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.View,
            ScopeType.Warehouse,
            warehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<PagedResult<AssetCustodyTimelineResponse>>(
                AssetErrors.NotFound(query.AssetId));
        }

        var page = await context.Custodies.AsNoTracking()
            .Where(item => item.AssetId == query.AssetId)
            .Select(item => new
            {
                item.Id,
                item.AssetId,
                item.HolderType,
                item.HolderId,
                item.CustodyKind,
                item.IssueDocumentId,
                item.ReturnDocumentId,
                item.Status,
                item.FromUtc,
                item.ToUtc,
                item.RowVersion
            })
            .OrderByDescending(item => item.FromUtc)
            .ThenBy(item => item.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        var items = page.Items
            .Select(item => new AssetCustodyTimelineResponse(
                item.Id,
                item.AssetId,
                item.HolderType.ToString(),
                item.HolderId,
                item.CustodyKind.ToString(),
                item.IssueDocumentId,
                item.ReturnDocumentId,
                item.Status.ToString(),
                item.FromUtc,
                item.ToUtc,
                item.RowVersion))
            .ToList();

        return new PagedResult<AssetCustodyTimelineResponse>(
            items,
            page.Page,
            page.PageSize,
            page.TotalItems);
    }
}

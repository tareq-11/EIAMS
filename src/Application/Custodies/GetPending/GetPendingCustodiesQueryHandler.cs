using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;
using Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Custodies.GetPending;

internal sealed class GetPendingCustodiesQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetPendingCustodiesQuery, PagedResult<PendingCustodyResponse>>
{
    public async Task<Result<PagedResult<PendingCustodyResponse>>> Handle(
        GetPendingCustodiesQuery query,
        CancellationToken cancellationToken)
    {
        bool exists = await context.Warehouses.AsNoTracking()
            .AnyAsync(item => item.Id == query.WarehouseId, cancellationToken);

        bool authorized = exists && await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.View,
            ScopeType.Warehouse,
            query.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<PagedResult<PendingCustodyResponse>>(
                WarehouseErrors.NotFound(query.WarehouseId));
        }

        var page = await (
                from custody in context.Custodies.AsNoTracking()
                join asset in context.Assets.AsNoTracking() on custody.AssetId equals asset.Id
                join issue in context.WarehouseDocuments.AsNoTracking() on custody.IssueDocumentId equals issue.Id
                where issue.WarehouseId == query.WarehouseId
                where custody.Status == CustodyStatus.Active && custody.CustodyKind == CustodyKind.Operational
                select new
                {
                    CustodyId = custody.Id,
                    AssetId = asset.Id,
                    asset.AssetNumber,
                    asset.MaterialId,
                    custody.HolderType,
                    custody.HolderId,
                    custody.IssueDocumentId,
                    custody.FromUtc,
                    custody.RowVersion
                })
            .OrderBy(item => item.FromUtc)
            .ThenBy(item => item.CustodyId)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        var items = page.Items
            .Select(item => new PendingCustodyResponse(
                item.CustodyId,
                item.AssetId,
                item.AssetNumber,
                item.MaterialId,
                item.HolderType.ToString(),
                item.HolderId,
                item.IssueDocumentId,
                item.FromUtc,
                item.RowVersion))
            .ToList();

        return new PagedResult<PendingCustodyResponse>(
            items,
            page.Page,
            page.PageSize,
            page.TotalItems);
    }
}

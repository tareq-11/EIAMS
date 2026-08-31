using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;
using Domain.StockMovements;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.StockMovements.GetList;

internal sealed class GetStockMovementsQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetStockMovementsQuery, PagedResult<StockMovementResponse>>
{
    public async Task<Result<PagedResult<StockMovementResponse>>> Handle(
        GetStockMovementsQuery query,
        CancellationToken cancellationToken)
    {
        WarehousePermissionScope access = await scopeAuthorizationService.GetWarehousePermissionScopeAsync(
            userContext.UserId,
            PermissionCodes.Inventory.View,
            cancellationToken);

        if (!access.HasEnterpriseAccess && access.WarehouseIds.Count == 0)
        {
            return Result.Failure<PagedResult<StockMovementResponse>>(StockMovementErrors.Forbidden);
        }

        IQueryable<StockMovementResponse> source = StockMovementQuerySupport.Build(
            context,
            access.HasEnterpriseAccess,
            access.WarehouseIds.ToArray(),
            query.WarehouseId,
            query.MaterialId,
            query.DocumentId,
            query.MovementType,
            query.FromUtc,
            query.ToUtc,
            query.Search);

        int totalItems = await source.CountAsync(cancellationToken);
        int offset = checked((query.Page - 1) * query.PageSize);
        List<StockMovementResponse> items = await source
            .Skip(offset)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StockMovementResponse>(items, query.Page, query.PageSize, totalItems);
    }
}

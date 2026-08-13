using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;
using Domain.Warehouses;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.StockMovements.GetByWarehouse;

internal sealed class GetStockMovementsByWarehouseQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetStockMovementsByWarehouseQuery, PagedResult<StockMovementResponse>>
{
    public async Task<Result<PagedResult<StockMovementResponse>>> Handle(
        GetStockMovementsByWarehouseQuery query,
        CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.View,
            ScopeType.Warehouse,
            query.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<PagedResult<StockMovementResponse>>(WarehouseErrors.NotFound(query.WarehouseId));
        }

        if (!await context.Warehouses.AnyAsync(w => w.Id == query.WarehouseId, cancellationToken))
        {
            return Result.Failure<PagedResult<StockMovementResponse>>(WarehouseErrors.NotFound(query.WarehouseId));
        }

        PagedResult<StockMovementResponse> movements = await context.StockMovements
            .Where(m => m.WarehouseId == query.WarehouseId)
            .Select(m => new StockMovementResponse
            {
                Id = m.Id,
                WarehouseId = m.WarehouseId,
                MaterialId = m.MaterialId,
                DocumentId = m.DocumentId,
                LineId = m.LineId,
                MovementType = m.MovementType.ToString(),
                QuantityDelta = m.QuantityDelta,
                PostedAtUtc = m.PostedAtUtc,
                PostedBy = m.PostedBy
            })
            .OrderByDescending(m => m.PostedAtUtc)
            .ThenBy(m => m.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return movements;
    }
}

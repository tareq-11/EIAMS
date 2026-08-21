using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.InventoryCounts;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.InventoryCounts.GetFreezeStatus;

internal sealed class GetInventoryFreezeStatusQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IInventoryFreezePolicyService freezePolicyService)
    : IQueryHandler<GetInventoryFreezeStatusQuery, InventoryFreezeStatusResponse>
{
    public async Task<Result<InventoryFreezeStatusResponse>> Handle(
        GetInventoryFreezeStatusQuery query,
        CancellationToken cancellationToken)
    {
        bool warehouseExists = await context.Warehouses.AsNoTracking()
            .AnyAsync(warehouse => warehouse.Id == query.WarehouseId, cancellationToken);

        if (!warehouseExists || !await scopeAuthorizationService.HasPermissionInScopeAsync(
                userContext.UserId,
                PermissionCodes.InventoryCounts.View,
                ScopeType.Warehouse,
                query.WarehouseId,
                cancellationToken))
        {
            return Result.Failure<InventoryFreezeStatusResponse>(
                WarehouseErrors.NotFound(query.WarehouseId));
        }

        InventoryFreezeEvaluation evaluation = await freezePolicyService.EvaluateAsync(
            [query.WarehouseId],
            cancellationToken);

        return new InventoryFreezeStatusResponse(
            query.WarehouseId,
            evaluation.BlockingError is not null,
            evaluation.Warnings.Count > 0,
            evaluation.ActiveCounts
                .Select(count => new ActiveInventoryFreezeResponse(count.CountId, count.FreezePolicy))
                .ToList());
    }
}

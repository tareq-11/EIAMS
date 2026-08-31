using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.StockMovements.GetList;
using Domain.Common;
using Domain.StockMovements;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.StockMovements.GetById;

internal sealed class GetStockMovementByIdQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetStockMovementByIdQuery, StockMovementResponse>
{
    public async Task<Result<StockMovementResponse>> Handle(
        GetStockMovementByIdQuery query,
        CancellationToken cancellationToken)
    {
        WarehousePermissionScope access = await scopeAuthorizationService.GetWarehousePermissionScopeAsync(
            userContext.UserId,
            PermissionCodes.Inventory.View,
            cancellationToken);

        if (!access.HasEnterpriseAccess && access.WarehouseIds.Count == 0)
        {
            return Result.Failure<StockMovementResponse>(StockMovementErrors.NotFound(query.MovementId));
        }

        StockMovementResponse? movement = await StockMovementQuerySupport.Build(
                context,
                access.HasEnterpriseAccess,
                access.WarehouseIds.ToArray(),
                movementId: query.MovementId)
            .SingleOrDefaultAsync(cancellationToken);

        return movement is null
            ? Result.Failure<StockMovementResponse>(StockMovementErrors.NotFound(query.MovementId))
            : movement;
    }
}

using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.Warehouses.GetById;

internal sealed class GetWarehouseByIdQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    HybridCache hybridCache)
    : IQueryHandler<GetWarehouseByIdQuery, WarehouseResponse>
{
    public async Task<Result<WarehouseResponse>> Handle(
        GetWarehouseByIdQuery query,
        CancellationToken cancellationToken)
    {
        WarehouseResponse? warehouse = await hybridCache.GetOrCreateAsync(
            $"warehouses:by-id:{query.WarehouseId}",
            async ct => await context.Warehouses
                .AsNoTracking()
                .Where(w => w.Id == query.WarehouseId)
                .Select(w => new WarehouseResponse
                {
                    Id = w.Id,
                    SiteId = w.SiteId,
                    Name = w.Name,
                    Code = w.Code,
                    WarehouseType = w.WarehouseType,
                    CanHoldStock = w.CanHoldStock,
                    Status = w.Status.ToString(),
                    RowVersion = w.RowVersion
                })
                .SingleOrDefaultAsync(ct),
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(10),
                LocalCacheExpiration = TimeSpan.FromMinutes(10)
            },
            tags: ["warehouses"],
            cancellationToken: cancellationToken);

        if (warehouse is null)
        {
            return Result.Failure<WarehouseResponse>(WarehouseErrors.NotFound(query.WarehouseId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Warehouses.View,
            ScopeType.Warehouse,
            warehouse.Id,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<WarehouseResponse>(WarehouseErrors.Forbidden);
        }

        return warehouse;
    }
}

using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.WarehouseCapabilities;
using Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.WarehouseCapabilities.GetByWarehouse;

internal sealed class GetWarehouseCapabilitiesQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetWarehouseCapabilitiesQuery, List<WarehouseCapabilityResponse>>
{
    public async Task<Result<List<WarehouseCapabilityResponse>>> Handle(
        GetWarehouseCapabilitiesQuery query,
        CancellationToken cancellationToken)
    {
        if (!await context.Warehouses.AnyAsync(w => w.Id == query.WarehouseId, cancellationToken))
        {
            return Result.Failure<List<WarehouseCapabilityResponse>>(WarehouseErrors.NotFound(query.WarehouseId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseCapabilities.Manage,
            ScopeType.Warehouse,
            query.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<List<WarehouseCapabilityResponse>>(WarehouseCapabilityErrors.Forbidden);
        }

        List<WarehouseCapabilityResponse> capabilities = await (
                from capability in context.WarehouseCapabilities
                where capability.WarehouseId == query.WarehouseId
                join materialDomain in context.MaterialDomains
                    on capability.MaterialDomainId equals materialDomain.Id
                select new WarehouseCapabilityResponse
                {
                    Id = capability.Id,
                    WarehouseId = capability.WarehouseId,
                    MaterialDomainId = capability.MaterialDomainId,
                    MaterialDomainCode = materialDomain.Code,
                    MaterialDomainName = materialDomain.Name,
                    Status = capability.Status.ToString()
                })
            .ToListAsync(cancellationToken);

        return capabilities;
    }
}

using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;
using Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Warehouses.GetList;

internal sealed class GetWarehousesQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetWarehousesQuery, PagedResult<WarehouseResponse>>
{
    public async Task<Result<PagedResult<WarehouseResponse>>> Handle(
        GetWarehousesQuery query,
        CancellationToken cancellationToken)
    {
        WarehousePermissionScope access = await scopeAuthorizationService.GetWarehousePermissionScopeAsync(
            userContext.UserId,
            PermissionCodes.Warehouses.View,
            cancellationToken);

        if (!access.HasEnterpriseAccess && access.WarehouseIds.Count == 0)
        {
            return Result.Failure<PagedResult<WarehouseResponse>>(WarehouseErrors.Forbidden);
        }

        PagedResult<WarehouseResponse> warehouses = await context.Warehouses
            .Where(w => access.HasEnterpriseAccess || access.WarehouseIds.Contains(w.Id))
            .Where(w => query.SiteId == null || w.SiteId == query.SiteId)
            .Where(w => query.Status == null || w.Status == query.Status)
            .Select(w => new WarehouseResponse
            {
                Id = w.Id,
                SiteId = w.SiteId,
                OrganizationalUnitId = w.OrganizationalUnitId,
                Name = w.Name,
                Code = w.Code,
                WarehouseType = w.WarehouseType,
                CanHoldStock = w.CanHoldStock,
                Status = w.Status.ToString(),
                RowVersion = w.RowVersion
            })
            .OrderBy(w => w.Name)
            .ThenBy(w => w.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return warehouses;
    }
}

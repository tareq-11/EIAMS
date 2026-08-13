using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;
using Domain.Warehouses;
using Domain.WarehouseMaterialSettings;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.WarehouseMaterialSettings.GetByWarehouse;

internal sealed class GetWarehouseMaterialSettingsQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetWarehouseMaterialSettingsQuery, PagedResult<WarehouseMaterialSettingResponse>>
{
    public async Task<Result<PagedResult<WarehouseMaterialSettingResponse>>> Handle(
        GetWarehouseMaterialSettingsQuery query,
        CancellationToken cancellationToken)
    {
        if (!await context.Warehouses.AnyAsync(w => w.Id == query.WarehouseId, cancellationToken))
        {
            return Result.Failure<PagedResult<WarehouseMaterialSettingResponse>>(
                WarehouseErrors.NotFound(query.WarehouseId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseMaterialSettings.Manage,
            ScopeType.Warehouse,
            query.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<PagedResult<WarehouseMaterialSettingResponse>>(
                WarehouseMaterialSettingErrors.Forbidden);
        }

        PagedResult<WarehouseMaterialSettingResponse> settings = await (
                from setting in context.WarehouseMaterialSettings
                where setting.WarehouseId == query.WarehouseId
                join material in context.Materials
                    on setting.MaterialId equals material.Id
                select new WarehouseMaterialSettingResponse
                {
                    Id = setting.Id,
                    WarehouseId = setting.WarehouseId,
                    MaterialId = setting.MaterialId,
                    MaterialCode = material.Code,
                    MaterialNameAr = material.NameAr,
                    MinQuantity = setting.MinQuantity,
                    MaxQuantity = setting.MaxQuantity,
                    Status = setting.Status.ToString()
                })
            .OrderBy(s => s.MaterialCode)
            .ThenBy(s => s.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return settings;
    }
}

using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
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
    : IQueryHandler<GetWarehouseMaterialSettingsQuery, List<WarehouseMaterialSettingResponse>>
{
    public async Task<Result<List<WarehouseMaterialSettingResponse>>> Handle(
        GetWarehouseMaterialSettingsQuery query,
        CancellationToken cancellationToken)
    {
        if (!await context.Warehouses.AnyAsync(w => w.Id == query.WarehouseId, cancellationToken))
        {
            return Result.Failure<List<WarehouseMaterialSettingResponse>>(WarehouseErrors.NotFound(query.WarehouseId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseMaterialSettings.Manage,
            ScopeType.Warehouse,
            query.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<List<WarehouseMaterialSettingResponse>>(WarehouseMaterialSettingErrors.Forbidden);
        }

        List<WarehouseMaterialSettingResponse> settings = await (
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
            .ToListAsync(cancellationToken);

        return settings;
    }
}

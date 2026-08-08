using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Materials;
using Domain.Warehouses;
using Domain.WarehouseMaterialSettings;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.WarehouseMaterialSettings.SetStatus;

internal sealed class SetWarehouseMaterialSettingStatusCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<SetWarehouseMaterialSettingStatusCommand>
{
    public async Task<Result> Handle(
        SetWarehouseMaterialSettingStatusCommand command,
        CancellationToken cancellationToken)
    {
        WarehouseMaterialSetting? setting = await context.WarehouseMaterialSettings
            .SingleOrDefaultAsync(s => s.Id == command.SettingId, cancellationToken);

        if (setting is null)
        {
            return Result.Failure(WarehouseMaterialSettingErrors.NotFound(command.SettingId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseMaterialSettings.Manage,
            ScopeType.Warehouse,
            setting.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure(WarehouseMaterialSettingErrors.Forbidden);
        }

        if (command.Status == Status.Active)
        {
            var prerequisites = await (
                    from warehouse in context.Warehouses
                    where warehouse.Id == setting.WarehouseId
                    join material in context.Materials on setting.MaterialId equals material.Id
                    select new { warehouse.CanHoldStock, MaterialStatus = material.Status })
                .SingleAsync(cancellationToken);

            if (!prerequisites.CanHoldStock)
            {
                return Result.Failure(
                    WarehouseMaterialSettingErrors.WarehouseCannotHoldStock(setting.WarehouseId));
            }

            if (prerequisites.MaterialStatus != MaterialStatus.Active)
            {
                return Result.Failure(WarehouseMaterialSettingErrors.MaterialNotActive(setting.MaterialId));
            }
        }

        setting.SetStatus(command.Status);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.WarehouseMaterialSettings;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.WarehouseMaterialSettings.Update;

internal sealed class UpdateWarehouseMaterialSettingCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<UpdateWarehouseMaterialSettingCommand>
{
    public async Task<Result> Handle(UpdateWarehouseMaterialSettingCommand command, CancellationToken cancellationToken)
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

        Result updateResult = setting.UpdateThresholds(command.MinQuantity, command.MaxQuantity);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

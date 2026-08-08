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

namespace Application.WarehouseMaterialSettings.Create;

internal sealed class CreateWarehouseMaterialSettingCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IDatabaseExceptionClassifier databaseExceptionClassifier)
    : ICommandHandler<CreateWarehouseMaterialSettingCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        CreateWarehouseMaterialSettingCommand command,
        CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseMaterialSettings.Manage,
            ScopeType.Warehouse,
            command.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(WarehouseMaterialSettingErrors.Forbidden);
        }

        Warehouse? warehouse = await context.Warehouses
            .SingleOrDefaultAsync(w => w.Id == command.WarehouseId, cancellationToken);

        if (warehouse is null)
        {
            return Result.Failure<Guid>(WarehouseErrors.NotFound(command.WarehouseId));
        }

        if (!warehouse.CanHoldStock)
        {
            return Result.Failure<Guid>(
                WarehouseMaterialSettingErrors.WarehouseCannotHoldStock(command.WarehouseId));
        }

        Material? material = await context.Materials
            .SingleOrDefaultAsync(m => m.Id == command.MaterialId, cancellationToken);

        if (material is null)
        {
            return Result.Failure<Guid>(WarehouseMaterialSettingErrors.MaterialNotFound(command.MaterialId));
        }

        if (material.Status != MaterialStatus.Active)
        {
            return Result.Failure<Guid>(WarehouseMaterialSettingErrors.MaterialNotActive(command.MaterialId));
        }

        bool alreadyExists = await context.WarehouseMaterialSettings.AnyAsync(
            s => s.WarehouseId == command.WarehouseId && s.MaterialId == command.MaterialId,
            cancellationToken);

        if (alreadyExists)
        {
            return Result.Failure<Guid>(WarehouseMaterialSettingErrors.AlreadyExists(
                command.WarehouseId,
                command.MaterialId));
        }

        Result<WarehouseMaterialSetting> settingResult = WarehouseMaterialSetting.Create(
            Guid.NewGuid(),
            command.WarehouseId,
            command.MaterialId,
            command.MinQuantity,
            command.MaxQuantity);

        if (settingResult.IsFailure)
        {
            return Result.Failure<Guid>(settingResult.Error);
        }

        context.WarehouseMaterialSettings.Add(settingResult.Value);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (databaseExceptionClassifier.IsUniqueConstraintViolation(exception))
        {
            return Result.Failure<Guid>(WarehouseMaterialSettingErrors.AlreadyExists(
                command.WarehouseId,
                command.MaterialId));
        }

        return settingResult.Value.Id;
    }
}

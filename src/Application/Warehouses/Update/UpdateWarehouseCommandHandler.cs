using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.OrganizationalUnits;
using Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.Warehouses.Update;

internal sealed class UpdateWarehouseCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    HybridCache hybridCache)
    : ICommandHandler<UpdateWarehouseCommand>
{
    public async Task<Result> Handle(UpdateWarehouseCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Warehouses.Manage,
            ScopeType.Warehouse,
            command.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure(WarehouseErrors.Forbidden);
        }

        Warehouse? warehouse = await context.Warehouses
            .SingleOrDefaultAsync(w => w.Id == command.WarehouseId, cancellationToken);

        if (warehouse is null)
        {
            return Result.Failure(WarehouseErrors.NotFound(command.WarehouseId));
        }

        if (warehouse.RowVersion != command.ExpectedRowVersion)
        {
            return Result.Failure(WarehouseErrors.RowVersionMismatch(
                command.WarehouseId,
                command.ExpectedRowVersion,
                warehouse.RowVersion));
        }

        bool canAccessNewOwner = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Warehouses.Manage,
            ScopeType.OrganizationalUnit,
            command.OrganizationalUnitId,
            cancellationToken);

        if (!canAccessNewOwner)
        {
            return Result.Failure(WarehouseErrors.Forbidden);
        }

        OrganizationalUnit? organizationalUnit = await context.OrganizationalUnits
            .AsNoTracking()
            .SingleOrDefaultAsync(unit => unit.Id == command.OrganizationalUnitId, cancellationToken);

        if (organizationalUnit is null)
        {
            return Result.Failure(WarehouseErrors.OrganizationalUnitNotFound(command.OrganizationalUnitId));
        }

        if (organizationalUnit.SiteId != warehouse.SiteId)
        {
            return Result.Failure(WarehouseErrors.OrganizationalUnitInDifferentSite(
                command.OrganizationalUnitId,
                warehouse.SiteId));
        }

        if (organizationalUnit.Status != Status.Active)
        {
            return Result.Failure(WarehouseErrors.OrganizationalUnitInactive(command.OrganizationalUnitId));
        }

        warehouse.UpdateDetailsAndAdministrativeOwner(
            command.Name,
            command.WarehouseType,
            command.CanHoldStock,
            command.OrganizationalUnitId);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            int? currentRowVersion = await context.Warehouses
                .AsNoTracking()
                .Where(w => w.Id == command.WarehouseId)
                .Select(w => (int?)w.RowVersion)
                .SingleOrDefaultAsync(cancellationToken);

            return Result.Failure(WarehouseErrors.RowVersionMismatch(
                command.WarehouseId,
                command.ExpectedRowVersion,
                currentRowVersion));
        }

        await hybridCache.RemoveByTagAsync("warehouses", cancellationToken);
        await hybridCache.RemoveByTagAsync("auth-roles", cancellationToken);

        return Result.Success();
    }
}

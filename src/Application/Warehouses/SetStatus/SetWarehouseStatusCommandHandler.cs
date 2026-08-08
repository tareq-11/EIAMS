using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Warehouses.SetStatus;

internal sealed class SetWarehouseStatusCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<SetWarehouseStatusCommand>
{
    public async Task<Result> Handle(SetWarehouseStatusCommand command, CancellationToken cancellationToken)
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

        warehouse.SetStatus(command.Status);

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

        return Result.Success();
    }
}

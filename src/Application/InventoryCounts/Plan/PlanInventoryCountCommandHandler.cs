using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.InventoryCounts;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.InventoryCounts;
using Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.InventoryCounts.Plan;

internal sealed class PlanInventoryCountCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IApplicationTransaction transaction,
    IWarehouseOperationLock warehouseOperationLock,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<PlanInventoryCountCommand, Guid>
{
    public Task<Result<Guid>> Handle(PlanInventoryCountCommand command, CancellationToken cancellationToken) =>
        transaction.ExecuteAsync(ct => HandleInTransactionAsync(command, ct), cancellationToken);

    private async Task<Result<Guid>> HandleInTransactionAsync(
        PlanInventoryCountCommand command,
        CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.InventoryCounts.Plan,
            ScopeType.Warehouse,
            command.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(WarehouseErrors.NotFound(command.WarehouseId));
        }

        Warehouse? warehouse = await context.Warehouses
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == command.WarehouseId, cancellationToken);

        if (warehouse is null)
        {
            return Result.Failure<Guid>(WarehouseErrors.NotFound(command.WarehouseId));
        }

        if (warehouse.Status != Status.Active || !warehouse.CanHoldStock)
        {
            return Result.Failure<Guid>(WarehouseErrors.CannotHoldStock(command.WarehouseId));
        }

        await warehouseOperationLock.AcquireAsync([command.WarehouseId], cancellationToken);

        var countId = Guid.NewGuid();
        Result<InventoryCount> countResult = InventoryCount.Plan(
            countId,
            command.WarehouseId,
            userContext.UserId,
            command.CountType,
            command.ScopeType,
            command.MaterialDomainId,
            command.FreezePolicy,
            dateTimeProvider.UtcNow);

        if (countResult.IsFailure)
        {
            return Result.Failure<Guid>(countResult.Error);
        }

        context.InventoryCounts.Add(countResult.Value);
        if (command.ScopeType == InventoryCountScopeType.SelectedMaterials)
        {
            context.InventoryCountScopeMaterials.AddRange(command.MaterialIds.Select(materialId =>
                InventoryCountScopeMaterial.Create(Guid.NewGuid(), countId, materialId)));
        }
        await context.SaveChangesAsync(cancellationToken);

        return countId;
    }
}

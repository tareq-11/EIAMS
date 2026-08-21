using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.InventoryCounts;
using Application.Abstractions.Warehouses;
using Domain.Common;
using Domain.InventoryCounts;
using Domain.Materials;
using Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.InventoryCounts.Plan;

internal sealed class PlanInventoryCountCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    ICapabilityCheckService capabilityCheckService,
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

        Guid[] heldMaterialIds = (await context.InventoryBalances.AsNoTracking()
                .Where(item => item.WarehouseId == command.WarehouseId && item.Quantity > 0)
                .Select(item => item.MaterialId)
                .ToListAsync(cancellationToken))
            .Concat(await context.AssetCurrentStatuses.AsNoTracking()
                .Where(item => item.WarehouseId == command.WarehouseId && item.CurrentStatus == AssetCurrentStatus.InStock)
                .Select(item => item.MaterialId)
                .ToListAsync(cancellationToken))
            .Distinct()
            .ToArray();

        var materialQuery =
            from material in context.Materials.AsNoTracking()
            join family in context.MaterialFamilies.AsNoTracking() on material.FamilyId equals family.Id
            join category in context.MaterialCategories.AsNoTracking() on family.CategoryId equals category.Id
            select new
            {
                Material = material,
                DomainId = category.MaterialDomainId
            };

        materialQuery = command.ScopeType switch
        {
            InventoryCountScopeType.MaterialDomain => materialQuery.Where(item =>
                item.DomainId == command.MaterialDomainId && heldMaterialIds.Contains(item.Material.Id)),
            InventoryCountScopeType.SelectedMaterials => materialQuery.Where(item =>
                command.MaterialIds.Contains(item.Material.Id)),
            _ => materialQuery.Where(item => heldMaterialIds.Contains(item.Material.Id))
        };

        var materials = await materialQuery.ToListAsync(cancellationToken);
        if (command.ScopeType == InventoryCountScopeType.SelectedMaterials &&
            materials.Count != command.MaterialIds.Distinct().Count())
        {
            return Result.Failure<Guid>(InventoryCountErrors.ScopeReferenceInvalid);
        }

        foreach (Guid domainId in materials.Select(item => item.DomainId).Distinct())
        {
            Result capability = await capabilityCheckService.EnsureAllowedAsync(
                command.WarehouseId,
                domainId,
                OperationType.Count,
                cancellationToken);

            if (capability.IsFailure)
            {
                return Result.Failure<Guid>(capability.Error);
            }
        }

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

        Guid[] materialIds = materials.Select(item => item.Material.Id).ToArray();
        Dictionary<Guid, decimal> balances = await context.InventoryBalances
            .AsNoTracking()
            .Where(item => item.WarehouseId == command.WarehouseId && materialIds.Contains(item.MaterialId))
            .ToDictionaryAsync(item => item.MaterialId, item => item.Quantity, cancellationToken);

        var assetRows = await context.AssetCurrentStatuses
            .AsNoTracking()
            .Where(item => item.WarehouseId == command.WarehouseId &&
                materialIds.Contains(item.MaterialId) &&
                item.CurrentStatus == AssetCurrentStatus.InStock)
            .Select(item => new { item.AssetId, item.MaterialId })
            .ToListAsync(cancellationToken);

        var lines = new List<InventoryCountLine>();
        foreach (var item in materials)
        {
            if (item.Material.IsAssetTracked)
            {
                foreach (var asset in assetRows.Where(asset => asset.MaterialId == item.Material.Id))
                {
                    Result<InventoryCountLine> lineResult = InventoryCountLine.Create(
                        Guid.NewGuid(), countId, item.Material.Id, asset.AssetId, 1m);
                    lines.Add(lineResult.Value);
                }
            }
            else
            {
                balances.TryGetValue(item.Material.Id, out decimal quantity);
                Result<InventoryCountLine> lineResult = InventoryCountLine.Create(
                    Guid.NewGuid(), countId, item.Material.Id, null, quantity);
                lines.Add(lineResult.Value);
            }
        }

        if (lines.Count == 0)
        {
            return Result.Failure<Guid>(InventoryCountErrors.SnapshotEmpty(countId));
        }

        context.InventoryCounts.Add(countResult.Value);
        context.InventoryCountLines.AddRange(lines);
        if (command.ScopeType == InventoryCountScopeType.SelectedMaterials)
        {
            context.InventoryCountScopeMaterials.AddRange(materialIds.Select(materialId =>
                InventoryCountScopeMaterial.Create(Guid.NewGuid(), countId, materialId)));
        }
        await context.SaveChangesAsync(cancellationToken);

        return countId;
    }
}

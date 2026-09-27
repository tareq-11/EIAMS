using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.InventoryCounts;
using Application.Abstractions.Messaging;
using Application.Abstractions.Warehouses;
using Domain.Common;
using Domain.InventoryCounts;
using Domain.Materials;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.InventoryCounts.ChangeStatus;

internal sealed class ChangeInventoryCountStatusCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IApplicationTransaction transaction,
    IWarehouseOperationLock warehouseOperationLock,
    ICapabilityCheckService capabilityCheckService,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ChangeInventoryCountStatusCommand>
{
    public async Task<Result> Handle(ChangeInventoryCountStatusCommand command, CancellationToken cancellationToken)
    {
        Result<bool> result = await transaction.ExecuteAsync(
            async ct =>
            {
                Result inner = await HandleCoreAsync(command, ct);
                return inner.IsFailure ? Result.Failure<bool>(inner.Error) : Result.Success(true);
            },
            cancellationToken);

        return result.IsFailure ? Result.Failure(result.Error) : Result.Success();
    }

    private async Task<Result> HandleCoreAsync(ChangeInventoryCountStatusCommand command, CancellationToken cancellationToken)
    {
        InventoryCount? count = await context.InventoryCounts
            .SingleOrDefaultAsync(item => item.Id == command.CountId, cancellationToken);

        string requiredPermission = command.TargetStatus switch
        {
            InventoryCountStatus.InProgress => PermissionCodes.InventoryCounts.Plan,
            InventoryCountStatus.Completed => PermissionCodes.InventoryCounts.Complete,
            InventoryCountStatus.Closed or InventoryCountStatus.Aborted => PermissionCodes.InventoryCounts.Close,
            _ => PermissionCodes.InventoryCounts.Complete
        };

        if (count is null || !await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId, requiredPermission,
            ScopeType.Warehouse, count.WarehouseId, cancellationToken))
        {
            return Result.Failure(InventoryCountErrors.NotFound(command.CountId));
        }

        if (count.RowVersion != command.ExpectedRowVersion)
        {
            return Result.Failure(InventoryCountErrors.RowVersionMismatch(
                count.Id, command.ExpectedRowVersion, count.RowVersion));
        }

        await warehouseOperationLock.AcquireAsync([count.WarehouseId], cancellationToken);

        int? currentRowVersion = await context.InventoryCounts.AsNoTracking()
            .Where(item => item.Id == count.Id)
            .Select(item => (int?)item.RowVersion)
            .SingleOrDefaultAsync(cancellationToken);
        if (currentRowVersion != command.ExpectedRowVersion)
        {
            return Result.Failure(InventoryCountErrors.RowVersionMismatch(
                count.Id, command.ExpectedRowVersion, currentRowVersion));
        }

        if (command.TargetStatus == InventoryCountStatus.InProgress &&
            await context.InventoryCounts.AnyAsync(item => item.WarehouseId == count.WarehouseId &&
                item.Id != count.Id && item.Status == InventoryCountStatus.InProgress, cancellationToken))
        {
            return Result.Failure(InventoryCountErrors.AnotherCountInProgress(count.WarehouseId));
        }

        if (command.TargetStatus == InventoryCountStatus.Completed &&
            await context.InventoryCountLines.AnyAsync(item => item.CountId == count.Id && item.ActualQuantity == null, cancellationToken))
        {
            return Result.Failure(InventoryCountErrors.ActualsIncomplete(count.Id));
        }

        if (command.TargetStatus == InventoryCountStatus.Closed &&
            await context.InventoryCountLines.AnyAsync(item => item.CountId == count.Id &&
                item.Difference != 0 && item.VarianceReason == null, cancellationToken))
        {
            return Result.Failure(InventoryCountErrors.VarianceReasonsRequired(count.Id));
        }

        if (command.TargetStatus == InventoryCountStatus.InProgress)
        {
            if (count.Status != InventoryCountStatus.Planned)
            {
                return Result.Failure(InventoryCountErrors.InvalidTransition(
                    count.Id, count.Status, command.TargetStatus));
            }

            // Rebuild while still Planned: membership is defined at Start, not when the count
            // was planned. This also replaces snapshots written by older versions at Plan time.
            List<InventoryCountLine> plannedLines = await context.InventoryCountLines
                .Where(line => line.CountId == count.Id)
                .ToListAsync(cancellationToken);
            if (plannedLines.Count > 0)
            {
                context.InventoryCountLines.RemoveRange(plannedLines);
                await context.SaveChangesAsync(cancellationToken);
            }

            Result<IReadOnlyCollection<InventoryCountLine>> membership = await BuildMembershipAsync(
                count, cancellationToken);
            if (membership.IsFailure)
            {
                return Result.Failure(membership.Error);
            }

            context.InventoryCountLines.AddRange(membership.Value);
            // Insert while the parent remains Planned. The database trigger takes the parent
            // row lock and rejects membership writes after the transition commits.
            await context.SaveChangesAsync(cancellationToken);
        }

        Result result = command.TargetStatus switch
        {
            InventoryCountStatus.InProgress => count.Start(dateTimeProvider.UtcNow),
            InventoryCountStatus.Completed => count.Complete(dateTimeProvider.UtcNow),
            InventoryCountStatus.Closed => count.Close(dateTimeProvider.UtcNow),
            InventoryCountStatus.Aborted => count.Abort(dateTimeProvider.UtcNow),
            _ => Result.Failure(InventoryCountErrors.InvalidTransition(count.Id, count.Status, command.TargetStatus))
        };

        if (result.IsFailure)
        {
            return result;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            int? current = await context.InventoryCounts.AsNoTracking()
                .Where(item => item.Id == count.Id)
                .Select(item => (int?)item.RowVersion)
                .SingleOrDefaultAsync(cancellationToken);
            return Result.Failure(InventoryCountErrors.RowVersionMismatch(
                count.Id, command.ExpectedRowVersion, current));
        }
        catch (DbUpdateException) when (command.TargetStatus == InventoryCountStatus.InProgress)
        {
            return Result.Failure(InventoryCountErrors.AnotherCountInProgress(count.WarehouseId));
        }
        return Result.Success();
    }

    private async Task<Result<IReadOnlyCollection<InventoryCountLine>>> BuildMembershipAsync(
        InventoryCount count,
        CancellationToken cancellationToken)
    {
        Guid[] heldMaterialIds = (await context.InventoryBalances.AsNoTracking()
                .Where(item => item.WarehouseId == count.WarehouseId && item.Quantity > 0)
                .Select(item => item.MaterialId)
                .ToListAsync(cancellationToken))
            .Concat(await context.AssetCurrentStatuses.AsNoTracking()
                .Where(item => item.WarehouseId == count.WarehouseId &&
                    item.CurrentStatus == AssetCurrentStatus.InStock)
                .Select(item => item.MaterialId)
                .ToListAsync(cancellationToken))
            .Distinct()
            .ToArray();

        Guid[] selectedMaterialIds = count.ScopeType == InventoryCountScopeType.SelectedMaterials
            ? await context.InventoryCountScopeMaterials.AsNoTracking()
                .Where(item => item.CountId == count.Id)
                .Select(item => item.MaterialId)
                .ToArrayAsync(cancellationToken)
            : [];

        var materialQuery =
            from material in context.Materials.AsNoTracking()
            join family in context.MaterialFamilies.AsNoTracking() on material.FamilyId equals family.Id
            join category in context.MaterialCategories.AsNoTracking() on family.CategoryId equals category.Id
            select new { Material = material, DomainId = category.MaterialDomainId };

        materialQuery = count.ScopeType switch
        {
            InventoryCountScopeType.MaterialDomain => materialQuery.Where(item =>
                item.DomainId == count.ScopeMaterialDomainId && heldMaterialIds.Contains(item.Material.Id)),
            InventoryCountScopeType.SelectedMaterials => materialQuery.Where(item =>
                selectedMaterialIds.Contains(item.Material.Id)),
            _ => materialQuery.Where(item => heldMaterialIds.Contains(item.Material.Id))
        };

        var materials = await materialQuery.ToListAsync(cancellationToken);
        if (count.ScopeType == InventoryCountScopeType.SelectedMaterials &&
            materials.Count != selectedMaterialIds.Length)
        {
            return Result.Failure<IReadOnlyCollection<InventoryCountLine>>(
                InventoryCountErrors.ScopeReferenceInvalid);
        }

        Result capability = await capabilityCheckService.EnsureAllowedBatchAsync(
            count.WarehouseId,
            materials.Select(item => item.DomainId).Distinct(),
            OperationType.Count,
            cancellationToken);
        if (capability.IsFailure)
        {
            return Result.Failure<IReadOnlyCollection<InventoryCountLine>>(capability.Error);
        }

        Guid[] materialIds = materials.Select(item => item.Material.Id).ToArray();
        Dictionary<Guid, decimal> balances = await context.InventoryBalances.AsNoTracking()
            .Where(item => item.WarehouseId == count.WarehouseId && materialIds.Contains(item.MaterialId))
            .ToDictionaryAsync(item => item.MaterialId, item => item.Quantity, cancellationToken);
        var assets = await context.AssetCurrentStatuses.AsNoTracking()
            .Where(item => item.WarehouseId == count.WarehouseId &&
                materialIds.Contains(item.MaterialId) && item.CurrentStatus == AssetCurrentStatus.InStock)
            .Select(item => new { item.AssetId, item.MaterialId })
            .ToListAsync(cancellationToken);

        var lines = new List<InventoryCountLine>();
        foreach (var item in materials)
        {
            if (item.Material.IsAssetTracked)
            {
                foreach (var asset in assets.Where(asset => asset.MaterialId == item.Material.Id))
                {
                    Result<InventoryCountLine> line = InventoryCountLine.Create(
                        Guid.NewGuid(), count.Id, item.Material.Id, asset.AssetId, 1m);
                    if (line.IsFailure)
                    {
                        return Result.Failure<IReadOnlyCollection<InventoryCountLine>>(line.Error);
                    }

                    lines.Add(line.Value);
                }
            }
            else
            {
                balances.TryGetValue(item.Material.Id, out decimal quantity);
                Result<InventoryCountLine> line = InventoryCountLine.Create(
                    Guid.NewGuid(), count.Id, item.Material.Id, null, quantity);
                if (line.IsFailure)
                {
                    return Result.Failure<IReadOnlyCollection<InventoryCountLine>>(line.Error);
                }

                lines.Add(line.Value);
            }
        }

        return lines.Count == 0
            ? Result.Failure<IReadOnlyCollection<InventoryCountLine>>(InventoryCountErrors.SnapshotEmpty(count.Id))
            : lines;
    }
}

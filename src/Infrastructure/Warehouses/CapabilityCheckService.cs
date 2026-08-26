using Application.Abstractions.Data;
using Application.Abstractions.Warehouses;
using Domain.Common;
using Domain.MaterialDomains;
using Domain.WarehouseCapabilities;
using Domain.WarehouseCapabilityOperations;
using Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.Warehouses;

internal sealed class CapabilityCheckService(IApplicationDbContext context) : ICapabilityCheckService
{
    public async Task<Result> EnsureAllowedAsync(
        Guid warehouseId,
        Guid materialDomainId,
        OperationType operationType,
        CancellationToken cancellationToken) =>
        await EnsureAllowedBatchAsync(warehouseId, [materialDomainId], operationType, cancellationToken);

    public async Task<Result> EnsureAllowedBatchAsync(
        Guid warehouseId,
        IEnumerable<Guid> materialDomainIds,
        OperationType operationType,
        CancellationToken cancellationToken)
    {
        Guid[] domainIds = materialDomainIds.Distinct().ToArray();
        if (domainIds.Length == 0)
        {
            return Result.Success();
        }

        Warehouse? warehouse = await context.Warehouses
            .AsNoTracking()
            .SingleOrDefaultAsync(w => w.Id == warehouseId, cancellationToken);

        if (warehouse is null)
        {
            return Result.Failure(WarehouseErrors.NotFound(warehouseId));
        }

        if (warehouse.Status != Status.Active)
        {
            return Result.Failure(WarehouseErrors.Inactive(warehouseId));
        }

        if (!warehouse.CanHoldStock)
        {
            return Result.Failure(WarehouseErrors.CannotHoldStock(warehouseId));
        }

        List<MaterialDomain> materialDomains = await context.MaterialDomains
            .AsNoTracking()
            .Where(d => domainIds.Contains(d.Id))
            .ToListAsync(cancellationToken);

        var domainById = materialDomains.ToDictionary(d => d.Id);

        foreach (Guid domainId in domainIds)
        {
            if (!domainById.TryGetValue(domainId, out MaterialDomain? domain))
            {
                return Result.Failure(WarehouseCapabilityErrors.MaterialDomainNotFound(domainId));
            }

            if (domain.Status != Status.Active)
            {
                return Result.Failure(WarehouseCapabilityErrors.MaterialDomainInactive(domainId));
            }
        }

        List<Guid> grantedOperations = await (
            from capability in context.WarehouseCapabilities.AsNoTracking()
            where capability.WarehouseId == warehouseId && domainIds.Contains(capability.MaterialDomainId) && capability.Status == Status.Active
            join operation in context.WarehouseCapabilityOperations.AsNoTracking() on capability.Id equals operation.CapabilityId
            where operation.OperationType == operationType
            select capability.MaterialDomainId
        ).ToListAsync(cancellationToken);

        var grantedDomainSet = grantedOperations.ToHashSet();

        foreach (Guid domainId in domainIds)
        {
            if (!grantedDomainSet.Contains(domainId))
            {
                // Check if capability exists to give exact error
                bool capabilityExists = await context.WarehouseCapabilities
                    .AsNoTracking()
                    .AnyAsync(c => c.WarehouseId == warehouseId && c.MaterialDomainId == domainId && c.Status == Status.Active, cancellationToken);

                if (!capabilityExists)
                {
                    return Result.Failure(WarehouseCapabilityErrors.NotGranted(warehouseId, domainId));
                }

                Guid capabilityId = await context.WarehouseCapabilities
                    .AsNoTracking()
                    .Where(c => c.WarehouseId == warehouseId && c.MaterialDomainId == domainId)
                    .Select(c => c.Id)
                    .FirstAsync(cancellationToken);

                return Result.Failure(WarehouseCapabilityOperationErrors.OperationNotGranted(capabilityId, operationType));
            }
        }

        return Result.Success();
    }
}

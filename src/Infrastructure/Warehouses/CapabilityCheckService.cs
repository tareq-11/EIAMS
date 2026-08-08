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
        CancellationToken cancellationToken)
    {
        Warehouse? warehouse = await context.Warehouses
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

        MaterialDomain? materialDomain = await context.MaterialDomains
            .SingleOrDefaultAsync(d => d.Id == materialDomainId, cancellationToken);

        if (materialDomain is null)
        {
            return Result.Failure(WarehouseCapabilityErrors.MaterialDomainNotFound(materialDomainId));
        }

        if (materialDomain.Status != Status.Active)
        {
            return Result.Failure(WarehouseCapabilityErrors.MaterialDomainInactive(materialDomainId));
        }

        WarehouseCapability? capability = await context.WarehouseCapabilities
            .SingleOrDefaultAsync(
                c => c.WarehouseId == warehouseId && c.MaterialDomainId == materialDomainId,
                cancellationToken);

        if (capability is null || capability.Status != Status.Active)
        {
            return Result.Failure(WarehouseCapabilityErrors.NotGranted(warehouseId, materialDomainId));
        }

        bool operationGranted = await context.WarehouseCapabilityOperations.AnyAsync(
            o => o.CapabilityId == capability.Id && o.OperationType == operationType,
            cancellationToken);

        if (!operationGranted)
        {
            return Result.Failure(WarehouseCapabilityOperationErrors.OperationNotGranted(capability.Id, operationType));
        }

        return Result.Success();
    }
}

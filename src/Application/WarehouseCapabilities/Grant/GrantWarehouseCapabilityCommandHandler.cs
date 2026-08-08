using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.MaterialDomains;
using Domain.WarehouseCapabilities;
using Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.WarehouseCapabilities.Grant;

internal sealed class GrantWarehouseCapabilityCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IDatabaseExceptionClassifier databaseExceptionClassifier)
    : ICommandHandler<GrantWarehouseCapabilityCommand, Guid>
{
    public async Task<Result<Guid>> Handle(GrantWarehouseCapabilityCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseCapabilities.Manage,
            ScopeType.Warehouse,
            command.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(WarehouseCapabilityErrors.Forbidden);
        }

        if (!await context.Warehouses.AnyAsync(w => w.Id == command.WarehouseId, cancellationToken))
        {
            return Result.Failure<Guid>(WarehouseErrors.NotFound(command.WarehouseId));
        }

        MaterialDomain? materialDomain = await context.MaterialDomains
            .SingleOrDefaultAsync(d => d.Id == command.MaterialDomainId, cancellationToken);

        if (materialDomain is null)
        {
            return Result.Failure<Guid>(WarehouseCapabilityErrors.MaterialDomainNotFound(command.MaterialDomainId));
        }

        if (materialDomain.Status != Status.Active)
        {
            return Result.Failure<Guid>(
                WarehouseCapabilityErrors.MaterialDomainInactive(command.MaterialDomainId));
        }

        WarehouseCapability? existing = await context.WarehouseCapabilities
            .SingleOrDefaultAsync(
                c => c.WarehouseId == command.WarehouseId && c.MaterialDomainId == command.MaterialDomainId,
                cancellationToken);

        if (existing is not null)
        {
            if (existing.Status == Status.Active)
            {
                return Result.Failure<Guid>(WarehouseCapabilityErrors.AlreadyGranted(
                    command.WarehouseId,
                    command.MaterialDomainId));
            }

            existing.SetStatus(Status.Active);

            await context.SaveChangesAsync(cancellationToken);

            return existing.Id;
        }

        var capability = WarehouseCapability.Create(
            Guid.NewGuid(),
            command.WarehouseId,
            command.MaterialDomainId);

        context.WarehouseCapabilities.Add(capability);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (databaseExceptionClassifier.IsUniqueConstraintViolation(exception))
        {
            return Result.Failure<Guid>(WarehouseCapabilityErrors.AlreadyGranted(
                command.WarehouseId,
                command.MaterialDomainId));
        }

        return capability.Id;
    }
}

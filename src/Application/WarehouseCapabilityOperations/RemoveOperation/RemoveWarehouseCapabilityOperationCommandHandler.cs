using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.WarehouseCapabilities;
using Domain.WarehouseCapabilityOperations;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.WarehouseCapabilityOperations.RemoveOperation;

internal sealed class RemoveWarehouseCapabilityOperationCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<RemoveWarehouseCapabilityOperationCommand>
{
    public async Task<Result> Handle(
        RemoveWarehouseCapabilityOperationCommand command,
        CancellationToken cancellationToken)
    {
        WarehouseCapability? capability = await context.WarehouseCapabilities
            .SingleOrDefaultAsync(c => c.Id == command.CapabilityId, cancellationToken);

        if (capability is null)
        {
            return Result.Failure(WarehouseCapabilityErrors.NotFound(command.CapabilityId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseCapabilities.Manage,
            ScopeType.Warehouse,
            capability.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure(WarehouseCapabilityOperationErrors.Forbidden);
        }

        if (capability.Status != Status.Active)
        {
            return Result.Failure(
                WarehouseCapabilityOperationErrors.CapabilityInactive(command.CapabilityId));
        }

        WarehouseCapabilityOperation? operation = await context.WarehouseCapabilityOperations
            .SingleOrDefaultAsync(
                o => o.CapabilityId == command.CapabilityId && o.OperationType == command.OperationType,
                cancellationToken);

        if (operation is null)
        {
            return Result.Failure(
                WarehouseCapabilityOperationErrors.OperationNotGranted(command.CapabilityId, command.OperationType));
        }

        operation.MarkAsRemoved();
        context.WarehouseCapabilityOperations.Remove(operation);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(WarehouseCapabilityOperationErrors.OperationNotGranted(
                command.CapabilityId,
                command.OperationType));
        }

        return Result.Success();
    }
}

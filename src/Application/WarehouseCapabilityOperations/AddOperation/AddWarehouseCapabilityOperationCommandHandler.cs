using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.WarehouseCapabilities;
using Domain.WarehouseCapabilityOperations;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.WarehouseCapabilityOperations.AddOperation;

internal sealed class AddWarehouseCapabilityOperationCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IDatabaseExceptionClassifier databaseExceptionClassifier)
    : ICommandHandler<AddWarehouseCapabilityOperationCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        AddWarehouseCapabilityOperationCommand command,
        CancellationToken cancellationToken)
    {
        WarehouseCapability? capability = await context.WarehouseCapabilities
            .SingleOrDefaultAsync(c => c.Id == command.CapabilityId, cancellationToken);

        if (capability is null)
        {
            return Result.Failure<Guid>(WarehouseCapabilityErrors.NotFound(command.CapabilityId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseCapabilities.Manage,
            ScopeType.Warehouse,
            capability.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(WarehouseCapabilityOperationErrors.Forbidden);
        }

        if (capability.Status != Status.Active)
        {
            return Result.Failure<Guid>(
                WarehouseCapabilityOperationErrors.CapabilityInactive(command.CapabilityId));
        }

        bool alreadyGranted = await context.WarehouseCapabilityOperations.AnyAsync(
            o => o.CapabilityId == command.CapabilityId && o.OperationType == command.OperationType,
            cancellationToken);

        if (alreadyGranted)
        {
            return Result.Failure<Guid>(WarehouseCapabilityOperationErrors.AlreadyGranted(
                command.CapabilityId,
                command.OperationType));
        }

        var operation = WarehouseCapabilityOperation.Create(
            Guid.NewGuid(),
            command.CapabilityId,
            command.OperationType);

        context.WarehouseCapabilityOperations.Add(operation);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (databaseExceptionClassifier.IsUniqueConstraintViolation(exception))
        {
            return Result.Failure<Guid>(WarehouseCapabilityOperationErrors.AlreadyGranted(
                command.CapabilityId,
                command.OperationType));
        }

        return operation.Id;
    }
}

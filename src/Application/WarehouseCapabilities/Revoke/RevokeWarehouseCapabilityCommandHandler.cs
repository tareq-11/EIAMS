using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.WarehouseCapabilities;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.WarehouseCapabilities.Revoke;

internal sealed class RevokeWarehouseCapabilityCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<RevokeWarehouseCapabilityCommand>
{
    public async Task<Result> Handle(RevokeWarehouseCapabilityCommand command, CancellationToken cancellationToken)
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
            return Result.Failure(WarehouseCapabilityErrors.Forbidden);
        }

        if (capability.Status != Status.Active)
        {
            return Result.Failure(WarehouseCapabilityErrors.AlreadyRevoked(command.CapabilityId));
        }

        capability.SetStatus(Status.Inactive);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

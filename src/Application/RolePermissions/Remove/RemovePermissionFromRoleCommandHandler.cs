using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Roles;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.RolePermissions.Remove;

internal sealed class RemovePermissionFromRoleCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<RemovePermissionFromRoleCommand>
{
    public async Task<Result> Handle(RemovePermissionFromRoleCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Roles.Manage,
            ScopeType.Enterprise,
            scopeId: null,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure(RoleErrors.Forbidden);
        }

        RolePermission? rolePermission = await context.RolePermissions
            .SingleOrDefaultAsync(
                rp => rp.RoleId == command.RoleId && rp.PermissionId == command.PermissionId,
                cancellationToken);

        if (rolePermission is null)
        {
            return Result.Failure(RolePermissionErrors.NotAssigned);
        }

        context.RolePermissions.Remove(rolePermission);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

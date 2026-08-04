using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Permissions;
using Domain.Roles;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.RolePermissions.Assign;

internal sealed class AssignPermissionToRoleCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<AssignPermissionToRoleCommand>
{
    public async Task<Result> Handle(AssignPermissionToRoleCommand command, CancellationToken cancellationToken)
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

        if (!await context.Roles.AnyAsync(r => r.Id == command.RoleId, cancellationToken))
        {
            return Result.Failure(RoleErrors.NotFound(command.RoleId));
        }

        if (!await context.Permissions.AnyAsync(p => p.Id == command.PermissionId, cancellationToken))
        {
            return Result.Failure(PermissionErrors.NotFound(command.PermissionId));
        }

        bool alreadyAssigned = await context.RolePermissions
            .AnyAsync(rp => rp.RoleId == command.RoleId && rp.PermissionId == command.PermissionId, cancellationToken);

        if (alreadyAssigned)
        {
            return Result.Failure(RolePermissionErrors.AlreadyAssigned);
        }

        context.RolePermissions.Add(RolePermission.Create(command.RoleId, command.PermissionId));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.Roles.Update;

internal sealed class UpdateRoleCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    HybridCache hybridCache)
    : ICommandHandler<UpdateRoleCommand>
{
    public async Task<Result> Handle(UpdateRoleCommand command, CancellationToken cancellationToken)
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

        Role? role = await context.Roles.SingleOrDefaultAsync(r => r.Id == command.RoleId, cancellationToken);

        if (role is null)
        {
            return Result.Failure(RoleErrors.NotFound(command.RoleId));
        }

        if (await context.Roles.AnyAsync(r => r.Id != command.RoleId && r.Name == command.Name, cancellationToken))
        {
            return Result.Failure(RoleErrors.NameNotUnique);
        }

        role.UpdateDetails(command.Name, command.Description);

        if (command.AllowedScopeTypes is not null)
        {
            ScopeType[] allowedScopeTypes = command.AllowedScopeTypes.Distinct().ToArray();

            bool invalidatesExistingAssignments = await context.UserRoleScopes
                .AsNoTracking()
                .AnyAsync(
                    assignment => assignment.RoleId == command.RoleId &&
                                  !allowedScopeTypes.Contains(assignment.ScopeType),
                    cancellationToken);

            if (invalidatesExistingAssignments)
            {
                return Result.Failure(RoleErrors.AllowedScopeTypesConflictWithAssignments(command.RoleId));
            }

            List<RoleAllowedScopeType> existingAllowedScopeTypes = await context.RoleAllowedScopeTypes
                .Where(item => item.RoleId == command.RoleId)
                .ToListAsync(cancellationToken);

            context.RoleAllowedScopeTypes.RemoveRange(existingAllowedScopeTypes);
            context.RoleAllowedScopeTypes.AddRange(
                allowedScopeTypes.Select(scopeType => RoleAllowedScopeType.Create(command.RoleId, scopeType)));
        }

        await context.SaveChangesAsync(cancellationToken);

        await hybridCache.RemoveByTagAsync("auth-roles", cancellationToken);

        return Result.Success();
    }
}

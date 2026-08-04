using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Roles;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Roles.Create;

internal sealed class CreateRoleCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<CreateRoleCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateRoleCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Roles.Manage,
            ScopeType.Enterprise,
            scopeId: null,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(RoleErrors.Forbidden);
        }

        if (await context.Roles.AnyAsync(r => r.Name == command.Name, cancellationToken))
        {
            return Result.Failure<Guid>(RoleErrors.NameNotUnique);
        }

        var role = Role.Create(Guid.NewGuid(), command.Name, command.Description);

        context.Roles.Add(role);

        await context.SaveChangesAsync(cancellationToken);

        return role.Id;
    }
}

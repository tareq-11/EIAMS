using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.OrganizationalUnits;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.OrganizationalUnits.Create;

internal sealed class CreateOrganizationalUnitCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<CreateOrganizationalUnitCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateOrganizationalUnitCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.OrganizationalUnits.Manage,
            ScopeType.Site,
            command.SiteId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(OrganizationalUnitErrors.Forbidden);
        }

        if (!await context.Sites.AnyAsync(s => s.Id == command.SiteId, cancellationToken))
        {
            return Result.Failure<Guid>(OrganizationalUnitErrors.SiteNotFound(command.SiteId));
        }

        if (command.ParentId is not null)
        {
            OrganizationalUnit? parent = await context.OrganizationalUnits
                .SingleOrDefaultAsync(u => u.Id == command.ParentId, cancellationToken);

            if (parent is null)
            {
                return Result.Failure<Guid>(OrganizationalUnitErrors.ParentNotFound(command.ParentId.Value));
            }

            if (parent.SiteId != command.SiteId)
            {
                return Result.Failure<Guid>(OrganizationalUnitErrors.ParentInDifferentSite(command.ParentId.Value));
            }
        }

        var unit = OrganizationalUnit.Create(Guid.NewGuid(), command.SiteId, command.ParentId, command.Name, command.UnitType);

        context.OrganizationalUnits.Add(unit);

        await context.SaveChangesAsync(cancellationToken);

        return unit.Id;
    }
}

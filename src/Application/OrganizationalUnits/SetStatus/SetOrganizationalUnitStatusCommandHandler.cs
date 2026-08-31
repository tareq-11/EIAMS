using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.OrganizationalUnits;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.OrganizationalUnits.SetStatus;

internal sealed class SetOrganizationalUnitStatusCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    HybridCache hybridCache)
    : ICommandHandler<SetOrganizationalUnitStatusCommand>
{
    public async Task<Result> Handle(SetOrganizationalUnitStatusCommand command, CancellationToken cancellationToken)
    {
        OrganizationalUnit? unit = await context.OrganizationalUnits
            .SingleOrDefaultAsync(u => u.Id == command.OrganizationalUnitId, cancellationToken);

        if (unit is null)
        {
            return Result.Failure(OrganizationalUnitErrors.NotFound(command.OrganizationalUnitId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.OrganizationalUnits.Manage,
            ScopeType.Site,
            unit.SiteId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure(OrganizationalUnitErrors.Forbidden);
        }

        unit.SetStatus(command.Status);

        await context.SaveChangesAsync(cancellationToken);
        await hybridCache.RemoveByTagAsync("organizational-units", cancellationToken);

        return Result.Success();
    }
}

using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Sites;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Sites.Update;

internal sealed class UpdateSiteCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<UpdateSiteCommand>
{
    public async Task<Result> Handle(UpdateSiteCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Sites.Manage,
            ScopeType.Enterprise,
            scopeId: null,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure(SiteErrors.Forbidden);
        }

        Site? site = await context.Sites.SingleOrDefaultAsync(s => s.Id == command.SiteId, cancellationToken);

        if (site is null)
        {
            return Result.Failure(SiteErrors.NotFound(command.SiteId));
        }

        site.UpdateDetails(command.Name, command.Location);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

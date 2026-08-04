using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Sites;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Sites.Create;

internal sealed class CreateSiteCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<CreateSiteCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateSiteCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Sites.Manage,
            ScopeType.Enterprise,
            scopeId: null,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(SiteErrors.Forbidden);
        }

        if (!await context.Organizations.AnyAsync(o => o.Id == command.OrganizationId, cancellationToken))
        {
            return Result.Failure<Guid>(SiteErrors.OrganizationNotFound(command.OrganizationId));
        }

        if (await context.Sites.AnyAsync(s => s.Code == command.Code, cancellationToken))
        {
            return Result.Failure<Guid>(SiteErrors.CodeNotUnique);
        }

        var site = Site.Create(Guid.NewGuid(), command.OrganizationId, command.Name, command.Code, command.Location);

        context.Sites.Add(site);

        await context.SaveChangesAsync(cancellationToken);

        return site.Id;
    }
}

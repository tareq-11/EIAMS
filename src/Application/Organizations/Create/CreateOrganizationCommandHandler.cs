using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Organizations.Create;

internal sealed class CreateOrganizationCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<CreateOrganizationCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateOrganizationCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Organizations.Manage,
            ScopeType.Enterprise,
            scopeId: null,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(OrganizationErrors.Forbidden);
        }

        if (await context.Organizations.AnyAsync(o => o.Code == command.Code, cancellationToken))
        {
            return Result.Failure<Guid>(OrganizationErrors.CodeNotUnique);
        }

        var organization = Organization.Create(Guid.NewGuid(), command.Name, command.Code);

        context.Organizations.Add(organization);

        await context.SaveChangesAsync(cancellationToken);

        return organization.Id;
    }
}

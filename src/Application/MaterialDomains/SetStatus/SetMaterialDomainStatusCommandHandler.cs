using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.MaterialDomains;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.MaterialDomains.SetStatus;

internal sealed class SetMaterialDomainStatusCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<SetMaterialDomainStatusCommand>
{
    public async Task<Result> Handle(SetMaterialDomainStatusCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.MaterialDomains.Manage,
            ScopeType.Enterprise,
            scopeId: null,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure(MaterialDomainErrors.Forbidden);
        }

        MaterialDomain? materialDomain = await context.MaterialDomains
            .SingleOrDefaultAsync(d => d.Id == command.MaterialDomainId, cancellationToken);

        if (materialDomain is null)
        {
            return Result.Failure(MaterialDomainErrors.NotFound(command.MaterialDomainId));
        }

        materialDomain.SetStatus(command.Status);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

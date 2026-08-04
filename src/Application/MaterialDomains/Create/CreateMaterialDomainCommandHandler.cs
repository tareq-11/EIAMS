using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.MaterialDomains;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.MaterialDomains.Create;

internal sealed class CreateMaterialDomainCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<CreateMaterialDomainCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateMaterialDomainCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.MaterialDomains.Manage,
            ScopeType.Enterprise,
            scopeId: null,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(MaterialDomainErrors.Forbidden);
        }

        if (await context.MaterialDomains.AnyAsync(d => d.Code == command.Code, cancellationToken))
        {
            return Result.Failure<Guid>(MaterialDomainErrors.CodeNotUnique);
        }

        var materialDomain = MaterialDomain.Create(Guid.NewGuid(), command.Name, command.Code);

        context.MaterialDomains.Add(materialDomain);

        await context.SaveChangesAsync(cancellationToken);

        return materialDomain.Id;
    }
}

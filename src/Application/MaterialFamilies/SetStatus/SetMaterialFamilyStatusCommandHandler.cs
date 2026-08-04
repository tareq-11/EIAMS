using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.MaterialFamilies;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.MaterialFamilies.SetStatus;

internal sealed class SetMaterialFamilyStatusCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<SetMaterialFamilyStatusCommand>
{
    public async Task<Result> Handle(SetMaterialFamilyStatusCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.MaterialFamilies.Manage,
            ScopeType.Enterprise,
            scopeId: null,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure(MaterialFamilyErrors.Forbidden);
        }

        MaterialFamily? family = await context.MaterialFamilies
            .SingleOrDefaultAsync(f => f.Id == command.MaterialFamilyId, cancellationToken);

        if (family is null)
        {
            return Result.Failure(MaterialFamilyErrors.NotFound(command.MaterialFamilyId));
        }

        family.SetStatus(command.Status);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.MaterialFamilies;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.MaterialFamilies.Create;

internal sealed class CreateMaterialFamilyCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<CreateMaterialFamilyCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateMaterialFamilyCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.MaterialFamilies.Manage,
            ScopeType.Enterprise,
            scopeId: null,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(MaterialFamilyErrors.Forbidden);
        }

        if (!await context.MaterialCategories.AnyAsync(c => c.Id == command.CategoryId, cancellationToken))
        {
            return Result.Failure<Guid>(MaterialFamilyErrors.CategoryNotFound(command.CategoryId));
        }

        if (!await context.UnitsOfMeasure.AnyAsync(u => u.Id == command.BaseUnitId, cancellationToken))
        {
            return Result.Failure<Guid>(MaterialFamilyErrors.BaseUnitNotFound(command.BaseUnitId));
        }

        var family = MaterialFamily.Create(Guid.NewGuid(), command.CategoryId, command.Name, command.Code, command.BaseUnitId);

        context.MaterialFamilies.Add(family);

        await context.SaveChangesAsync(cancellationToken);

        return family.Id;
    }
}

using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.MaterialCategories;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.MaterialCategories.SetStatus;

internal sealed class SetMaterialCategoryStatusCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<SetMaterialCategoryStatusCommand>
{
    public async Task<Result> Handle(SetMaterialCategoryStatusCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.MaterialCategories.Manage,
            ScopeType.Enterprise,
            scopeId: null,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure(MaterialCategoryErrors.Forbidden);
        }

        MaterialCategory? category = await context.MaterialCategories
            .SingleOrDefaultAsync(c => c.Id == command.MaterialCategoryId, cancellationToken);

        if (category is null)
        {
            return Result.Failure(MaterialCategoryErrors.NotFound(command.MaterialCategoryId));
        }

        category.SetStatus(command.Status);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

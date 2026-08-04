using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.MaterialCategories;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.MaterialCategories.Update;

internal sealed class UpdateMaterialCategoryCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<UpdateMaterialCategoryCommand>
{
    public async Task<Result> Handle(UpdateMaterialCategoryCommand command, CancellationToken cancellationToken)
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

        category.UpdateDetails(command.Name, command.Code);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

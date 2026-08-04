using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.MaterialCategories;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.MaterialCategories.Move;

internal sealed class MoveMaterialCategoryCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<MoveMaterialCategoryCommand>
{
    public async Task<Result> Handle(MoveMaterialCategoryCommand command, CancellationToken cancellationToken)
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
            .SingleOrDefaultAsync(candidate => candidate.Id == command.MaterialCategoryId, cancellationToken);

        if (category is null)
        {
            return Result.Failure(MaterialCategoryErrors.NotFound(command.MaterialCategoryId));
        }

        if (command.ParentCategoryId is Guid parentCategoryId)
        {
            MaterialCategory? parent = await context.MaterialCategories
                .SingleOrDefaultAsync(candidate => candidate.Id == parentCategoryId, cancellationToken);

            if (parent is null)
            {
                return Result.Failure(MaterialCategoryErrors.ParentNotFound(parentCategoryId));
            }

            if (parent.MaterialDomainId != category.MaterialDomainId)
            {
                return Result.Failure(MaterialCategoryErrors.ParentInDifferentDomain(parentCategoryId));
            }

            if (await WouldCreateCycleAsync(category.Id, parent, cancellationToken))
            {
                return Result.Failure(MaterialCategoryErrors.CircularParent);
            }
        }

        category.MoveTo(command.ParentCategoryId);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<bool> WouldCreateCycleAsync(
        Guid categoryId,
        MaterialCategory candidateParent,
        CancellationToken cancellationToken)
    {
        MaterialCategory? current = candidateParent;
        HashSet<Guid> visited = [];

        while (current is not null)
        {
            if (current.Id == categoryId || !visited.Add(current.Id))
            {
                return true;
            }

            if (current.ParentCategoryId is not Guid parentId)
            {
                return false;
            }

            current = await context.MaterialCategories
                .SingleOrDefaultAsync(category => category.Id == parentId, cancellationToken);
        }

        return false;
    }
}

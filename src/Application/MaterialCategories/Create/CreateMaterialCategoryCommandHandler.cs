using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.MaterialCategories;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.MaterialCategories.Create;

internal sealed class CreateMaterialCategoryCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<CreateMaterialCategoryCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateMaterialCategoryCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.MaterialCategories.Manage,
            ScopeType.Enterprise,
            scopeId: null,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(MaterialCategoryErrors.Forbidden);
        }

        if (!await context.MaterialDomains.AnyAsync(d => d.Id == command.MaterialDomainId, cancellationToken))
        {
            return Result.Failure<Guid>(MaterialCategoryErrors.MaterialDomainNotFound(command.MaterialDomainId));
        }

        if (command.ParentCategoryId is not null)
        {
            MaterialCategory? parent = await context.MaterialCategories
                .SingleOrDefaultAsync(c => c.Id == command.ParentCategoryId, cancellationToken);

            if (parent is null)
            {
                return Result.Failure<Guid>(MaterialCategoryErrors.ParentNotFound(command.ParentCategoryId.Value));
            }

            if (parent.MaterialDomainId != command.MaterialDomainId)
            {
                return Result.Failure<Guid>(MaterialCategoryErrors.ParentInDifferentDomain(command.ParentCategoryId.Value));
            }
        }

        var category = MaterialCategory.Create(
            Guid.NewGuid(),
            command.MaterialDomainId,
            command.ParentCategoryId,
            command.Name,
            command.Code);

        context.MaterialCategories.Add(category);

        await context.SaveChangesAsync(cancellationToken);

        return category.Id;
    }
}

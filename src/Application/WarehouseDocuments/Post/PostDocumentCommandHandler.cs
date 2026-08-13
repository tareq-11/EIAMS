using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Posting;
using Domain.Common;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.WarehouseDocuments.Post;

internal sealed class PostDocumentCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IDocumentPostingCoordinator postingCoordinator)
    : ICommandHandler<PostDocumentCommand>
{
    public async Task<Result> Handle(PostDocumentCommand command, CancellationToken cancellationToken)
    {
        WarehouseDocument? document = await context.WarehouseDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == command.DocumentId, cancellationToken);

        if (document is null)
        {
            return Result.Failure(WarehouseDocumentErrors.NotFound(command.DocumentId));
        }

        bool hasReview = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.Review,
            ScopeType.Warehouse,
            document.WarehouseId,
            cancellationToken);

        if (!hasReview)
        {
            return Result.Failure(WarehouseDocumentErrors.NotFound(command.DocumentId));
        }

        // A reversal document requires review (the routine post action) plus reverse (specific
        // authorization to negate already-posted movements) - M3-PLAN.md §4.6.
        if (document.ReversalOfDocumentId is not null)
        {
            bool hasReverse = await scopeAuthorizationService.HasPermissionInScopeAsync(
                userContext.UserId,
                PermissionCodes.WarehouseDocuments.Reverse,
                ScopeType.Warehouse,
                document.WarehouseId,
                cancellationToken);

            if (!hasReverse)
            {
                return Result.Failure(WarehouseDocumentErrors.NotFound(command.DocumentId));
            }
        }

        Result<Guid> postResult = await postingCoordinator.PostAsync(
            command.DocumentId,
            command.ExpectedRowVersion,
            userContext.UserId,
            cancellationToken);

        return postResult.IsFailure ? Result.Failure(postResult.Error) : Result.Success();
    }
}

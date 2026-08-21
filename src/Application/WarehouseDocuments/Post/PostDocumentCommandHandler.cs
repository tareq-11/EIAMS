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
    IDocumentPostingScopeResolver postingScopeResolver,
    IDocumentPostingCoordinator postingCoordinator)
    : ICommandHandler<PostDocumentCommand, PostDocumentResponse>
{
    public async Task<Result<PostDocumentResponse>> Handle(
        PostDocumentCommand command,
        CancellationToken cancellationToken)
    {
        WarehouseDocument? document = await context.WarehouseDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == command.DocumentId, cancellationToken);

        if (document is null)
        {
            return Result.Failure<PostDocumentResponse>(WarehouseDocumentErrors.NotFound(command.DocumentId));
        }

        bool hasReview = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.Review,
            ScopeType.Warehouse,
            document.WarehouseId,
            cancellationToken);

        if (!hasReview)
        {
            return Result.Failure<PostDocumentResponse>(WarehouseDocumentErrors.NotFound(command.DocumentId));
        }

        Result<IReadOnlyCollection<Guid>> scopeResult = await postingScopeResolver.ResolveAsync(
            document,
            cancellationToken);

        if (scopeResult.IsFailure)
        {
            return Result.Failure<PostDocumentResponse>(scopeResult.Error);
        }

        foreach (Guid warehouseId in scopeResult.Value.Where(id => id != document.WarehouseId))
        {
            bool canReviewDestination = await scopeAuthorizationService.HasPermissionInScopeAsync(
                userContext.UserId,
                PermissionCodes.WarehouseDocuments.Review,
                ScopeType.Warehouse,
                warehouseId,
                cancellationToken);

            if (!canReviewDestination)
            {
                return Result.Failure<PostDocumentResponse>(WarehouseDocumentErrors.NotFound(command.DocumentId));
            }
        }

        // A reversal document requires review (the routine post action) plus reverse (specific
        // authorization to negate already-posted movements) - M3-PLAN.md §4.6.
        if (document.ReversalOfDocumentId is not null)
        {
            foreach (Guid warehouseId in scopeResult.Value)
            {
                bool hasReverse = await scopeAuthorizationService.HasPermissionInScopeAsync(
                    userContext.UserId,
                    PermissionCodes.WarehouseDocuments.Reverse,
                    ScopeType.Warehouse,
                    warehouseId,
                    cancellationToken);

                if (!hasReverse)
                {
                    return Result.Failure<PostDocumentResponse>(WarehouseDocumentErrors.NotFound(command.DocumentId));
                }
            }
        }

        Result<PostingOutcome> postResult = await postingCoordinator.PostAsync(
            command.DocumentId,
            command.ExpectedRowVersion,
            userContext.UserId,
            cancellationToken);

        if (postResult.IsFailure)
        {
            return Result.Failure<PostDocumentResponse>(postResult.Error);
        }

        return new PostDocumentResponse(
            postResult.Value.DocumentId,
            postResult.Value.Warnings
                .Select(warning => new PostDocumentWarningResponse(
                    warning.Code,
                    warning.Message,
                    warning.CountId,
                    warning.WarehouseId))
                .ToList());
    }
}

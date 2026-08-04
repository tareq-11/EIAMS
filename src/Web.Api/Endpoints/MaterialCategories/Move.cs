using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialCategories.Move;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MaterialCategories;

internal sealed class Move : IEndpoint
{
    public sealed record Request(Guid? ParentCategoryId);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("material-categories/{materialCategoryId:guid}/parent", async (
            Guid materialCategoryId,
            Request request,
            ICommandHandler<MoveMaterialCategoryCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new MoveMaterialCategoryCommand(materialCategoryId, request.ParentCategoryId),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.MaterialCategories.Manage)
        .WithTags(Tags.MaterialCategories);
    }
}

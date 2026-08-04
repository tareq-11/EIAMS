using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialCategories.SetStatus;
using Domain.Common;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MaterialCategories;

internal sealed class SetStatus : IEndpoint
{
    public sealed record Request(int Status);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("material-categories/{materialCategoryId:guid}/status", async (
            Guid materialCategoryId,
            Request request,
            ICommandHandler<SetMaterialCategoryStatusCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new SetMaterialCategoryStatusCommand(materialCategoryId, (Status)request.Status);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.MaterialCategories.Manage)
        .WithTags(Tags.MaterialCategories);
    }
}

using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialCategories.Update;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MaterialCategories;

internal sealed class Update : IEndpoint
{
    public sealed record Request(string Name, string Code);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("material-categories/{materialCategoryId:guid}", async (
            Guid materialCategoryId,
            Request request,
            ICommandHandler<UpdateMaterialCategoryCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateMaterialCategoryCommand(materialCategoryId, request.Name, request.Code);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.MaterialCategories.Manage)
        .WithTags(Tags.MaterialCategories);
    }
}

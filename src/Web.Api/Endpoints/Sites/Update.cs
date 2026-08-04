using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Sites.Update;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Sites;

internal sealed class Update : IEndpoint
{
    public sealed record Request(string Name, string? Location);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("sites/{siteId:guid}", async (
            Guid siteId,
            Request request,
            ICommandHandler<UpdateSiteCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateSiteCommand(siteId, request.Name, request.Location);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Sites.Manage)
        .WithTags(Tags.Sites);
    }
}

using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Sites.SetStatus;
using Domain.Common;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Sites;

internal sealed class SetStatus : IEndpoint
{
    public sealed record Request(int Status);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("sites/{siteId:guid}/status", async (
            Guid siteId,
            Request request,
            ICommandHandler<SetSiteStatusCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new SetSiteStatusCommand(siteId, (Status)request.Status);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Sites.Manage)
        .WithTags(Tags.Sites);
    }
}

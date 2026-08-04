using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Sites.Create;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Sites;

internal sealed class Create : IEndpoint
{
    public sealed record Request(Guid OrganizationId, string Name, string Code, string? Location);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("sites", async (
            Request request,
            ICommandHandler<CreateSiteCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateSiteCommand(request.OrganizationId, request.Name, request.Code, request.Location);

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Sites.Manage)
        .WithTags(Tags.Sites);
    }
}

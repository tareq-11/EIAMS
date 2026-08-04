using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Organizations.Update;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Organizations;

internal sealed class Update : IEndpoint
{
    public sealed record Request(string Name);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("organizations/{organizationId:guid}", async (
            Guid organizationId,
            Request request,
            ICommandHandler<UpdateOrganizationCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateOrganizationCommand(organizationId, request.Name);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Organizations.Manage)
        .WithTags(Tags.Organizations);
    }
}

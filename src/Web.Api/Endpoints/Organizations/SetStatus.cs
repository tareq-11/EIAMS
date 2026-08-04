using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Organizations.SetStatus;
using Domain.Common;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Organizations;

internal sealed class SetStatus : IEndpoint
{
    public sealed record Request(int Status);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("organizations/{organizationId:guid}/status", async (
            Guid organizationId,
            Request request,
            ICommandHandler<SetOrganizationStatusCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new SetOrganizationStatusCommand(organizationId, (Status)request.Status);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Organizations.Manage)
        .WithTags(Tags.Organizations);
    }
}

using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.OrganizationalUnits.SetStatus;
using Domain.Common;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.OrganizationalUnits;

internal sealed class SetStatus : IEndpoint
{
    public sealed record Request(int Status);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("organizational-units/{organizationalUnitId:guid}/status", async (
            Guid organizationalUnitId,
            Request request,
            ICommandHandler<SetOrganizationalUnitStatusCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new SetOrganizationalUnitStatusCommand(organizationalUnitId, (Status)request.Status);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.OrganizationalUnits.Manage)
        .WithTags(Tags.OrganizationalUnits);
    }
}

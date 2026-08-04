using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.OrganizationalUnits.Update;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.OrganizationalUnits;

internal sealed class Update : IEndpoint
{
    public sealed record Request(string Name, string UnitType);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("organizational-units/{organizationalUnitId:guid}", async (
            Guid organizationalUnitId,
            Request request,
            ICommandHandler<UpdateOrganizationalUnitCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateOrganizationalUnitCommand(organizationalUnitId, request.Name, request.UnitType);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.OrganizationalUnits.Manage)
        .WithTags(Tags.OrganizationalUnits);
    }
}

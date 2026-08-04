using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.OrganizationalUnits.Create;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.OrganizationalUnits;

internal sealed class Create : IEndpoint
{
    public sealed record Request(Guid SiteId, Guid? ParentId, string Name, string UnitType);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("organizational-units", async (
            Request request,
            ICommandHandler<CreateOrganizationalUnitCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateOrganizationalUnitCommand(
                request.SiteId,
                request.ParentId,
                request.Name,
                request.UnitType);

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.OrganizationalUnits.Manage)
        .WithTags(Tags.OrganizationalUnits);
    }
}

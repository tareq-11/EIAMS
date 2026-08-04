using Application.Abstractions.Messaging;
using Application.OrganizationalUnits.GetById;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.OrganizationalUnits;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("organizational-units/{organizationalUnitId:guid}", async (
            Guid organizationalUnitId,
            IQueryHandler<GetOrganizationalUnitByIdQuery, OrganizationalUnitResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetOrganizationalUnitByIdQuery(organizationalUnitId);

            Result<OrganizationalUnitResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.OrganizationalUnits);
    }
}

using Application.Abstractions.Messaging;
using Application.OrganizationalUnits.GetList;
using Domain.Common;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.OrganizationalUnits;

internal sealed class GetList : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("organizational-units", async (
            Guid? siteId,
            Guid? parentId,
            Status? status,
            IQueryHandler<GetOrganizationalUnitsQuery, List<OrganizationalUnitResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetOrganizationalUnitsQuery(siteId, parentId, status);

            Result<List<OrganizationalUnitResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.OrganizationalUnits);
    }
}

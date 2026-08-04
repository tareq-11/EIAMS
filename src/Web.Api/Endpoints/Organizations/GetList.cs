using Application.Abstractions.Messaging;
using Application.Organizations.GetList;
using Domain.Common;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Organizations;

internal sealed class GetList : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("organizations", async (
            Status? status,
            IQueryHandler<GetOrganizationsQuery, List<OrganizationResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetOrganizationsQuery(status);

            Result<List<OrganizationResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Organizations);
    }
}

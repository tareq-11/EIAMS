using Application.Abstractions.Messaging;
using Application.Organizations.GetById;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Organizations;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("organizations/{organizationId:guid}", async (
            Guid organizationId,
            IQueryHandler<GetOrganizationByIdQuery, OrganizationResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetOrganizationByIdQuery(organizationId);

            Result<OrganizationResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Organizations);
    }
}

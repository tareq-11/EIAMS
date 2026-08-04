using Application.Abstractions.Messaging;
using Application.Sites.GetList;
using Domain.Common;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Sites;

internal sealed class GetList : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("sites", async (
            Guid? organizationId,
            Status? status,
            IQueryHandler<GetSitesQuery, List<SiteResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetSitesQuery(organizationId, status);

            Result<List<SiteResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Sites);
    }
}

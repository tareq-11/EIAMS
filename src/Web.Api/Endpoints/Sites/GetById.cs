using Application.Abstractions.Messaging;
using Application.Sites.GetById;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Sites;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("sites/{siteId:guid}", async (
            Guid siteId,
            IQueryHandler<GetSiteByIdQuery, SiteResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetSiteByIdQuery(siteId);

            Result<SiteResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Sites);
    }
}

using Application.Abstractions.Messaging;
using Application.MaterialDomains.GetList;
using Domain.Common;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MaterialDomains;

internal sealed class GetList : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("material-domains", async (
            Status? status,
            IQueryHandler<GetMaterialDomainsQuery, List<MaterialDomainResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetMaterialDomainsQuery(status);

            Result<List<MaterialDomainResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.MaterialDomains);
    }
}

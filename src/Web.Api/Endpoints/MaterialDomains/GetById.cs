using Application.Abstractions.Messaging;
using Application.MaterialDomains.GetById;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MaterialDomains;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("material-domains/{materialDomainId:guid}", async (
            Guid materialDomainId,
            IQueryHandler<GetMaterialDomainByIdQuery, MaterialDomainResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetMaterialDomainByIdQuery(materialDomainId);

            Result<MaterialDomainResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.MaterialDomains);
    }
}

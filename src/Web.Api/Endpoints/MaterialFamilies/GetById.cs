using Application.Abstractions.Messaging;
using Application.MaterialFamilies.GetById;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MaterialFamilies;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("material-families/{materialFamilyId:guid}", async (
            Guid materialFamilyId,
            IQueryHandler<GetMaterialFamilyByIdQuery, MaterialFamilyResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetMaterialFamilyByIdQuery(materialFamilyId);

            Result<MaterialFamilyResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.MaterialFamilies);
    }
}

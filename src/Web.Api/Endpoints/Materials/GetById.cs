using Application.Abstractions.Messaging;
using Application.Materials.GetById;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Materials;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("materials/{materialId:guid}", async (
            Guid materialId,
            IQueryHandler<GetMaterialByIdQuery, MaterialResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetMaterialByIdQuery(materialId);

            Result<MaterialResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Materials);
    }
}

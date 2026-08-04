using Application.Abstractions.Messaging;
using Application.MaterialUnitConversions.GetByMaterial;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MaterialUnitConversions;

internal sealed class GetByMaterial : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("materials/{materialId:guid}/unit-conversions", async (
            Guid materialId,
            IQueryHandler<GetMaterialUnitConversionsQuery, List<MaterialUnitConversionResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetMaterialUnitConversionsQuery(materialId);

            Result<List<MaterialUnitConversionResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.MaterialUnitConversions);
    }
}

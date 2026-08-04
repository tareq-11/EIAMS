using Application.Abstractions.Messaging;
using Application.UnitsOfMeasure.GetList;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.UnitsOfMeasure;

internal sealed class GetList : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("units-of-measure", async (
            IQueryHandler<GetUnitsOfMeasureQuery, List<UnitOfMeasureResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetUnitsOfMeasureQuery();

            Result<List<UnitOfMeasureResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.UnitsOfMeasure);
    }
}

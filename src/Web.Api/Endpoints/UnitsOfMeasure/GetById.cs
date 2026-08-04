using Application.Abstractions.Messaging;
using Application.UnitsOfMeasure.GetById;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.UnitsOfMeasure;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("units-of-measure/{unitOfMeasureId:guid}", async (
            Guid unitOfMeasureId,
            IQueryHandler<GetUnitOfMeasureByIdQuery, UnitOfMeasureResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetUnitOfMeasureByIdQuery(unitOfMeasureId);

            Result<UnitOfMeasureResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.UnitsOfMeasure);
    }
}

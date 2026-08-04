using Application.Abstractions.Messaging;
using Application.MaterialCategories.GetById;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MaterialCategories;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("material-categories/{materialCategoryId:guid}", async (
            Guid materialCategoryId,
            IQueryHandler<GetMaterialCategoryByIdQuery, MaterialCategoryResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetMaterialCategoryByIdQuery(materialCategoryId);

            Result<MaterialCategoryResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.MaterialCategories);
    }
}

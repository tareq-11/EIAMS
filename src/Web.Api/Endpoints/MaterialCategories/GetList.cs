using Application.Abstractions.Messaging;
using Application.MaterialCategories.GetList;
using Domain.Common;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MaterialCategories;

internal sealed class GetList : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("material-categories", async (
            Guid? materialDomainId,
            Guid? parentCategoryId,
            bool rootOnly,
            Status? status,
            IQueryHandler<GetMaterialCategoriesQuery, List<MaterialCategoryResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetMaterialCategoriesQuery(materialDomainId, parentCategoryId, rootOnly, status);

            Result<List<MaterialCategoryResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.MaterialCategories);
    }
}

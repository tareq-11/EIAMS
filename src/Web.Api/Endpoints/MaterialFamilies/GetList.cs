using Application.Abstractions.Messaging;
using Application.MaterialFamilies.GetList;
using Domain.Common;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MaterialFamilies;

internal sealed class GetList : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("material-families", async (
            Guid? categoryId,
            Status? status,
            IQueryHandler<GetMaterialFamiliesQuery, List<MaterialFamilyResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetMaterialFamiliesQuery(categoryId, status);

            Result<List<MaterialFamilyResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.MaterialFamilies);
    }
}

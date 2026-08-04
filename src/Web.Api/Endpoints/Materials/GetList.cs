using Application.Abstractions.Messaging;
using Application.Materials.GetList;
using Domain.Materials;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Materials;

internal sealed class GetList : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("materials", async (
            Guid? familyId,
            Guid? materialDomainId,
            MaterialStatus? status,
            IQueryHandler<GetMaterialsQuery, List<MaterialResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetMaterialsQuery(familyId, materialDomainId, status);

            Result<List<MaterialResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Materials);
    }
}

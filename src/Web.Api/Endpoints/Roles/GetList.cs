using Application.Abstractions.Messaging;
using Application.Roles.GetList;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Roles;

internal sealed class GetList : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("roles", async (
            IQueryHandler<GetRolesQuery, List<RoleResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetRolesQuery();

            Result<List<RoleResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Roles);
    }
}

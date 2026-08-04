using Application.Abstractions.Messaging;
using Application.Roles.GetById;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Roles;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("roles/{roleId:guid}", async (
            Guid roleId,
            IQueryHandler<GetRoleByIdQuery, RoleResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetRoleByIdQuery(roleId);

            Result<RoleResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Roles);
    }
}

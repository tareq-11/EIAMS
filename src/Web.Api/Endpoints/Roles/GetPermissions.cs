using Application.Abstractions.Messaging;
using Application.RolePermissions.GetByRole;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Roles;

internal sealed class GetPermissions : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("roles/{roleId:guid}/permissions", async (
            Guid roleId,
            IQueryHandler<GetRolePermissionsQuery, List<PermissionResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetRolePermissionsQuery(roleId);

            Result<List<PermissionResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Roles);
    }
}

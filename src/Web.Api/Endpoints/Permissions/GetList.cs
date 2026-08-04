using Application.Abstractions.Messaging;
using Application.Permissions.GetList;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Permissions;

internal sealed class GetList : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("permissions", async (
            IQueryHandler<GetPermissionsQuery, List<PermissionResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetPermissionsQuery();

            Result<List<PermissionResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Permissions);
    }
}

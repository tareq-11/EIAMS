using Application.Abstractions.Messaging;
using Application.UserRoleScopes.GetByUser;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.UserRoleScopes;

internal sealed class GetByUser : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("users/{userId:guid}/role-scopes", async (
            Guid userId,
            IQueryHandler<GetUserRoleScopesQuery, List<UserRoleScopeResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetUserRoleScopesQuery(userId);

            Result<List<UserRoleScopeResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.UserRoleScopes);
    }
}

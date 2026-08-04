using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.UserRoleScopes.Revoke;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.UserRoleScopes;

internal sealed class Revoke : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("user-role-scopes/{userRoleScopeId:guid}", async (
            Guid userRoleScopeId,
            ICommandHandler<RevokeUserRoleScopeCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new RevokeUserRoleScopeCommand(userRoleScopeId);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Roles.Manage)
        .WithTags(Tags.UserRoleScopes);
    }
}

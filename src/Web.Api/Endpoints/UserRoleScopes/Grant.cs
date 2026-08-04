using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.UserRoleScopes.Grant;
using Domain.Common;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.UserRoleScopes;

internal sealed class Grant : IEndpoint
{
    public sealed record Request(Guid UserId, Guid RoleId, int ScopeType, Guid? ScopeId);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("user-role-scopes", async (
            Request request,
            ICommandHandler<GrantUserRoleScopeCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new GrantUserRoleScopeCommand(
                request.UserId,
                request.RoleId,
                (ScopeType)request.ScopeType,
                request.ScopeId);

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Roles.Manage)
        .WithTags(Tags.UserRoleScopes);
    }
}

using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.RolePermissions.Remove;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Roles;

internal sealed class RemovePermission : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("roles/{roleId:guid}/permissions/{permissionId:guid}", async (
            Guid roleId,
            Guid permissionId,
            ICommandHandler<RemovePermissionFromRoleCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new RemovePermissionFromRoleCommand(roleId, permissionId);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Roles.Manage)
        .WithTags(Tags.Roles);
    }
}

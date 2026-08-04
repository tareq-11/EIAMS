using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.RolePermissions.Assign;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Roles;

internal sealed class AssignPermission : IEndpoint
{
    public sealed record Request(Guid PermissionId);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("roles/{roleId:guid}/permissions", async (
            Guid roleId,
            Request request,
            ICommandHandler<AssignPermissionToRoleCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new AssignPermissionToRoleCommand(roleId, request.PermissionId);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Roles.Manage)
        .WithTags(Tags.Roles);
    }
}

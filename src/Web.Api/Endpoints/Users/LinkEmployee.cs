using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Users.LinkEmployee;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

internal sealed class LinkEmployee : IEndpoint
{
    public sealed record Request(Guid EmployeeId);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("users/{userId:guid}/employee", async (
            Guid userId,
            Request request,
            ICommandHandler<LinkUserToEmployeeCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new LinkUserToEmployeeCommand(userId, request.EmployeeId),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Employees.Manage)
        .WithTags(Tags.Users);
    }
}

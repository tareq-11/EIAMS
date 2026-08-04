using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Employees.SetStatus;
using Domain.Common;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Employees;

internal sealed class SetStatus : IEndpoint
{
    public sealed record Request(int Status);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("employees/{employeeId:guid}/status", async (
            Guid employeeId,
            Request request,
            ICommandHandler<SetEmployeeStatusCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new SetEmployeeStatusCommand(employeeId, (Status)request.Status);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Employees.Manage)
        .WithTags(Tags.Employees);
    }
}

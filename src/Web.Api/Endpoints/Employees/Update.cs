using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Employees.Update;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Employees;

internal sealed class Update : IEndpoint
{
    public sealed record Request(string FullName, string? JobTitle);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("employees/{employeeId:guid}", async (
            Guid employeeId,
            Request request,
            ICommandHandler<UpdateEmployeeCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateEmployeeCommand(employeeId, request.FullName, request.JobTitle);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Employees.Manage)
        .WithTags(Tags.Employees);
    }
}

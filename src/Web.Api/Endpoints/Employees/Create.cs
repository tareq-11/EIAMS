using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Employees.Create;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Employees;

internal sealed class Create : IEndpoint
{
    public sealed record Request(Guid OrgUnitId, string FullName, string EmployeeNumber, string? JobTitle);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("employees", async (
            Request request,
            ICommandHandler<CreateEmployeeCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateEmployeeCommand(
                request.OrgUnitId,
                request.FullName,
                request.EmployeeNumber,
                request.JobTitle);

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Employees.Manage)
        .WithTags(Tags.Employees);
    }
}

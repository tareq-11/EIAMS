using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Employees.Update;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Employees;

[ApiController]
[Route("employees")]
[Tags(Tags.Employees)]
public sealed class UpdateEmployeeController(ICommandHandler<UpdateEmployeeCommand> handler) : ControllerBase
{
    public sealed record RequestBody(string FullName, string? JobTitle);

    [HttpPut("{employeeId:guid}")]
    [HasPermission(PermissionCodes.Employees.Manage)]
    public async Task<IResult> Handle(Guid employeeId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new UpdateEmployeeCommand(employeeId, request.FullName, request.JobTitle);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}

using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Employees.SetStatus;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Employees;

[ApiController]
[Route("employees")]
[Tags(Tags.Employees)]
public sealed class SetEmployeeStatusController(ICommandHandler<SetEmployeeStatusCommand> handler) : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] int Status);

    [HttpPut("{employeeId:guid}/status")]
    [HasPermission(PermissionCodes.Employees.Manage)]
    public async Task<IResult> Handle(Guid employeeId, RequestBody request, CancellationToken cancellationToken)
    {
        var command = new SetEmployeeStatusCommand(employeeId, (Status)request.Status);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}

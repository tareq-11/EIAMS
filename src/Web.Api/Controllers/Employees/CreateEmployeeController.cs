using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Employees.Create;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Employees;

[ApiController]
[Route("employees")]
[Tags(Tags.Employees)]
public sealed class CreateEmployeeController(ICommandHandler<CreateEmployeeCommand, Guid> handler) : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] Guid OrgUnitId, string FullName, string EmployeeNumber, string? JobTitle);

    [HttpPost]
    [HasPermission(PermissionCodes.Employees.Manage)]
    public async Task<IResult> Handle(RequestBody request, CancellationToken cancellationToken)
    {
        var command = new CreateEmployeeCommand(
            request.OrgUnitId,
            request.FullName,
            request.EmployeeNumber,
            request.JobTitle);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}

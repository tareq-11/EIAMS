using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Users.LinkEmployee;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Users;

[ApiController]
[Route("users")]
[Tags(Tags.Users)]
public sealed class LinkEmployeeController(ICommandHandler<LinkUserToEmployeeCommand> handler) : ControllerBase
{
    public sealed record RequestBody([property: JsonRequired] Guid EmployeeId);

    [HttpPut("{userId:guid}/employee")]
    [HasPermission(PermissionCodes.Employees.Manage)]
    public async Task<IResult> Handle(Guid userId, RequestBody request, CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(
            new LinkUserToEmployeeCommand(userId, request.EmployeeId),
            cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}

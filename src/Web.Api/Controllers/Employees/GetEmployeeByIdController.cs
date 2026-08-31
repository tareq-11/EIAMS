using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Employees.GetById;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Employees;

[ApiController]
[Route("employees")]
[Tags(Tags.Employees)]
public sealed class GetEmployeeByIdController(IQueryHandler<GetEmployeeByIdQuery, EmployeeResponse> handler)
    : ControllerBase
{
    [HttpGet("{employeeId:guid}")]
    [ProducesResponseType<ApiResponse<EmployeeResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.Employees.View)]
    public async Task<IResult> Handle(Guid employeeId, CancellationToken cancellationToken)
    {
        var query = new GetEmployeeByIdQuery(employeeId);

        Result<EmployeeResponse> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}

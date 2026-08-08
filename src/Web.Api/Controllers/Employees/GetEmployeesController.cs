using Application.Abstractions.Messaging;
using Application.Employees.GetList;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Employees;

[ApiController]
[Route("employees")]
[Tags(Tags.Employees)]
public sealed class GetEmployeesController(IQueryHandler<GetEmployeesQuery, List<EmployeeResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<IResult> Handle(Guid? orgUnitId, Status? status, CancellationToken cancellationToken)
    {
        var query = new GetEmployeesQuery(orgUnitId, status);

        Result<List<EmployeeResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}

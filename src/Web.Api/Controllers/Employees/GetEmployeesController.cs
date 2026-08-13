using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
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
public sealed class GetEmployeesController(IQueryHandler<GetEmployeesQuery, PagedResult<EmployeeResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType<ApiResponse<IReadOnlyList<EmployeeResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IResult> Handle(
        Guid? orgUnitId,
        Status? status,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetEmployeesQuery(orgUnitId, status, pagination.Page, pagination.PageSize);

        Result<PagedResult<EmployeeResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}

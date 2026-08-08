using Application.Abstractions.Messaging;
using Application.Warehouses.GetList;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Warehouses;

[ApiController]
[Route("warehouses")]
[Tags(Tags.Warehouses)]
public sealed class GetWarehousesController(IQueryHandler<GetWarehousesQuery, List<WarehouseResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType<ApiResponse<List<WarehouseResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IResult> Handle(Guid? siteId, Status? status, CancellationToken cancellationToken)
    {
        var query = new GetWarehousesQuery(siteId, status);

        Result<List<WarehouseResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}

using Application.Abstractions.Messaging;
using Application.Warehouses.GetById;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Warehouses;

[ApiController]
[Route("warehouses")]
[Tags(Tags.Warehouses)]
public sealed class GetWarehouseByIdController(IQueryHandler<GetWarehouseByIdQuery, WarehouseResponse> handler)
    : ControllerBase
{
    [HttpGet("{warehouseId:guid}")]
    [Authorize]
    [ProducesResponseType<ApiResponse<WarehouseResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Handle(Guid warehouseId, CancellationToken cancellationToken)
    {
        var query = new GetWarehouseByIdQuery(warehouseId);

        Result<WarehouseResponse> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}

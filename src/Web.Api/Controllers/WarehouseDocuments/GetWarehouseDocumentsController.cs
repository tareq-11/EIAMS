using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.WarehouseDocuments.GetList;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.WarehouseDocuments;

[ApiController]
[Route("warehouse-documents")]
[Tags(Tags.WarehouseDocuments)]
public sealed class GetWarehouseDocumentsController(
    IQueryHandler<GetWarehouseDocumentsQuery, PagedResult<WarehouseDocumentResponse>> handler)
    : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType<ApiResponse<IReadOnlyList<WarehouseDocumentResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IResult> Handle(
        Guid? warehouseId,
        Guid? siteId,
        DocumentType? documentType,
        DocumentStatus? documentStatus,
        string? systemReferenceNumber,
        string? paperDocumentNumber,
        DateTime? fromDateUtc,
        DateTime? toDateUtc,
        [FromQuery] PaginationQueryParameters pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetWarehouseDocumentsQuery(
            warehouseId,
            siteId,
            documentType,
            documentStatus,
            systemReferenceNumber,
            paperDocumentNumber,
            fromDateUtc,
            toDateUtc,
            pagination.Page,
            pagination.PageSize);

        Result<PagedResult<WarehouseDocumentResponse>> result = await handler.Handle(query, cancellationToken);

        return result.ToPaginatedApiResponse(HttpContext);
    }
}

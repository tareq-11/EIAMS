using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.ReceivingInfos.GetSupplierSuggestions;
using Application.ReceivingInfos;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.ReceivingInfos;

[ApiController]
[Route("receiving-infos")]
[Tags(Tags.ReceivingInfos)]
public sealed class GetSupplierSuggestionsController(
    IQueryHandler<GetSupplierSuggestionsQuery, IReadOnlyList<SupplierSuggestionResponse>> handler)
    : ControllerBase
{
    [HttpGet("suppliers")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<SupplierSuggestionResponse>>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.WarehouseDocuments.View)]
    public async Task<IResult> Handle(string? search, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<SupplierSuggestionResponse>> result = await handler.Handle(
            new GetSupplierSuggestionsQuery(search), cancellationToken);
        return result.ToApiResponse(HttpContext);
    }
}

using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.ReceivingInfos.Upsert;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.ReceivingInfos;

[ApiController]
[Route("warehouse-documents/{documentId:guid}/receiving-info")]
[Tags(Tags.WarehouseDocuments)]
public sealed class UpsertReceivingInfoController(ICommandHandler<UpsertReceivingInfoCommand> handler)
    : ControllerBase
{
    public sealed record RequestBody(
        [property: JsonRequired] string SupplierRef,
        string? SupplierInvoiceRef,
        [property: JsonRequired] ReceivingType ReceivingType,
        [property: JsonRequired] int ExpectedRowVersion);

    [HttpPut]
    [HasPermission(PermissionCodes.WarehouseDocuments.Edit)]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(
        Guid documentId,
        RequestBody request,
        CancellationToken cancellationToken)
    {
        var command = new UpsertReceivingInfoCommand(
            documentId,
            request.SupplierRef,
            request.SupplierInvoiceRef,
            request.ReceivingType,
            request.ExpectedRowVersion);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}

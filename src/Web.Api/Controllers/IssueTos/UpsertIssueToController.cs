using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.IssueTos.Upsert;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.IssueTos;

[ApiController]
[Route("warehouse-documents/{documentId:guid}/issue-to")]
[Tags(Tags.WarehouseDocuments)]
public sealed class UpsertIssueToController(ICommandHandler<UpsertIssueToCommand> handler) : ControllerBase
{
    public sealed record RequestBody(
        [property: JsonRequired] PartyType RecipientType,
        [property: JsonRequired] Guid RecipientId,
        [property: JsonRequired] string IssueReason,
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
        var command = new UpsertIssueToCommand(
            documentId,
            request.RecipientType,
            request.RecipientId,
            request.IssueReason,
            request.ExpectedRowVersion);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}

using System.Text.Json.Serialization;
using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Custodies.Transfer;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Custodies;

public sealed record TransferCustodyRequest(
    [property: JsonRequired, JsonConverter(typeof(JsonStringEnumConverter))] CustodySubjectType SubjectType,
    [property: JsonRequired, JsonConverter(typeof(JsonStringEnumConverter))] PartyType NewHolderType,
    [property: JsonRequired] Guid NewHolderId,
    [property: JsonRequired, JsonConverter(typeof(JsonStringEnumConverter))] CustodyKind NewCustodyKind,
    [property: JsonRequired] int ExpectedRowVersion,
    string? Note = null);

[ApiController]
[Route("custodies/{custodyId:guid}/transfer")]
[Tags(Tags.Assets)]
public sealed class TransferCustodyController(
    ICommandHandler<TransferCustodyCommand> handler)
    : ControllerBase
{
    [HttpPost]
    [HasPermission(PermissionCodes.Custodies.Manage)]
    [ProducesResponseType<ApiResponse<EmptyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Handle(
        [FromRoute] Guid custodyId,
        [FromBody] TransferCustodyRequest request,
        CancellationToken cancellationToken)
    {
        var command = new TransferCustodyCommand(
            custodyId,
            request.SubjectType,
            request.NewHolderType,
            request.NewHolderId,
            request.NewCustodyKind,
            request.ExpectedRowVersion,
            request.Note);

        Result result = await handler.Handle(command, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}

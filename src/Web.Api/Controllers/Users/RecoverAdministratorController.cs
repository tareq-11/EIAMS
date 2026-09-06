using Application.Abstractions.Messaging;
using Application.Users.RecoverAdministrator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Users;

[ApiController]
[AllowAnonymous]
[Route("admin/recovery")]
[Tags(Tags.Users)]
public sealed class RecoverAdministratorController(
    ICommandHandler<RecoverAdministratorCommand, Guid> handler) : ControllerBase
{
    public sealed record RequestBody(string Email, string FirstName, string LastName, string Password);

    public sealed record ResponseBody(Guid Id);

    [HttpPost("administrator")]
    [RequestSizeLimit(AuthRequestLimits.MaximumBodySize)]
    [EnableRateLimiting(RateLimitingPolicies.Authentication)]
    [ProducesResponseType<ApiResponse<ResponseBody>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IResult> Handle(
        RequestBody request,
        [FromHeader(Name = "X-Administrator-Recovery-Token")] string? recoveryToken,
        CancellationToken cancellationToken)
    {
        var command = new RecoverAdministratorCommand(
            request.Email,
            request.FirstName,
            request.LastName,
            request.Password,
            recoveryToken);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.Match(
            id => ApiResults.Created(HttpContext, $"/api/v1/admin/users/{id}", new ResponseBody(id)),
            failure => CustomResults.Problem(failure, HttpContext));
    }
}

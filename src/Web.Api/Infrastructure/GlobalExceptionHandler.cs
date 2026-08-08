using Microsoft.AspNetCore.Diagnostics;

namespace Web.Api.Infrastructure;

internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception occurred");

        IResult result = ApiResults.Error(
            httpContext,
            StatusCodes.Status500InternalServerError,
            "SERVER_FAILURE",
            "An unexpected server error occurred.");

        await result.ExecuteAsync(httpContext);

        return true;
    }
}

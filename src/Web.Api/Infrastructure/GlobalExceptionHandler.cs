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
        if (exception is BadHttpRequestException badRequestException)
        {
            logger.LogWarning(
                exception,
                "HTTP request was rejected with status code {StatusCode}",
                badRequestException.StatusCode);

            IResult badRequestResult = badRequestException.StatusCode == StatusCodes.Status413PayloadTooLarge
                ? ApiResults.Error(
                    httpContext,
                    StatusCodes.Status413PayloadTooLarge,
                    "REQUEST_BODY_TOO_LARGE",
                    "The request body exceeds the configured maximum size.")
                : ApiResults.ErrorFromStatusCode(httpContext, badRequestException.StatusCode);

            await badRequestResult.ExecuteAsync(httpContext);

            return true;
        }

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

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
                "HTTP request was rejected with status code {StatusCode} and exception type {ExceptionType}",
                badRequestException.StatusCode,
                badRequestException.GetType().Name);

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

        logger.LogError(
            exception,
            "Unhandled exception of type {ExceptionType} occurred for request {RequestId}",
            exception.GetType().Name,
            ApiRequestContext.GetRequestId(httpContext));

        IResult result = ApiResults.ErrorFromStatusCode(
            httpContext,
            StatusCodes.Status500InternalServerError);

        await result.ExecuteAsync(httpContext);

        return true;
    }
}

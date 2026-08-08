using Microsoft.Extensions.Primitives;
using Serilog.Context;
using Web.Api.Infrastructure;

namespace Web.Api.Middleware;

public class RequestContextLoggingMiddleware(RequestDelegate next)
{
    private const string CorrelationIdHeaderName = "Correlation-Id";
    private const string RequestIdHeaderName = "X-Request-Id";

    public Task Invoke(HttpContext context)
    {
        string requestId = GetRequestId(context);
        context.Items[ApiRequestContext.RequestIdItemKey] = requestId;
        context.Response.Headers[RequestIdHeaderName] = requestId;

        using (LogContext.PushProperty("CorrelationId", requestId))
        {
            return next.Invoke(context);
        }
    }

    private static string GetRequestId(HttpContext context)
    {
        context.Request.Headers.TryGetValue(
            CorrelationIdHeaderName,
            out StringValues correlationId);

        string? suppliedId = correlationId.FirstOrDefault();

        return Guid.TryParse(suppliedId, out Guid parsedId)
            ? parsedId.ToString()
            : Guid.NewGuid().ToString();
    }
}

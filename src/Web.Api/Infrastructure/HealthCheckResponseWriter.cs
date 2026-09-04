using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Web.Api.Infrastructure;

internal static class HealthCheckResponseWriter
{
    internal static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        return JsonSerializer.SerializeAsync(
            context.Response.Body,
            new Dictionary<string, string> { ["status"] = report.Status.ToString() },
            cancellationToken: context.RequestAborted);
    }
}

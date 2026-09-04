using Microsoft.Extensions.Primitives;

namespace Web.Api.Extensions;

internal static class SecurityHeadersExtensions
{
    internal static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                IHeaderDictionary headers = context.Response.Headers;
                headers.TryAdd("X-Content-Type-Options", new StringValues("nosniff"));
                headers.TryAdd("X-Frame-Options", new StringValues("DENY"));
                headers.TryAdd("Referrer-Policy", new StringValues("no-referrer"));
                headers.TryAdd("Content-Security-Policy", new StringValues("default-src 'none'; frame-ancestors 'none'"));
                headers.TryAdd("Permissions-Policy", new StringValues("camera=(), microphone=(), geolocation=()"));

                if (context.Request.Path.StartsWithSegments("/api/v1/auth"))
                {
                    headers.TryAdd("Cache-Control", new StringValues("no-store"));
                    headers.TryAdd("Pragma", new StringValues("no-cache"));
                }

                return Task.CompletedTask;
            });

            await next(context);
        });
}

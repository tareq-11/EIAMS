using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Web.Api.Infrastructure;

namespace Web.Api.Extensions;

internal static class RateLimitingExtensions
{
    internal static IServiceCollection AddRateLimitingInternal(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        int globalPermitLimit = configuration.GetValue<int?>("RateLimiting:Global:PermitLimit") ?? 100;
        int globalWindowSeconds = configuration.GetValue<int?>("RateLimiting:Global:WindowInSeconds") ?? 60;
        int authPermitLimit = configuration.GetValue<int?>("RateLimiting:Authentication:PermitLimit") ?? 10;
        int authWindowSeconds = configuration.GetValue<int?>("RateLimiting:Authentication:WindowInSeconds") ?? 60;
        int authConcurrencyLimit = configuration.GetValue<int?>("RateLimiting:Authentication:ConcurrencyLimit") ?? 4;
        int authGlobalConcurrencyLimit = configuration.GetValue<int?>(
            "RateLimiting:Authentication:GlobalConcurrencyLimit") ?? 16;
        int reportingConcurrencyLimit = configuration.GetValue<int?>("RateLimiting:Concurrency:Reporting") ?? 8;
        int uploadConcurrencyLimit = configuration.GetValue<int?>("RateLimiting:Concurrency:Upload") ?? 2;
        int postingConcurrencyLimit = configuration.GetValue<int?>("RateLimiting:Concurrency:Posting") ?? 4;
        int healthPermitLimit = configuration.GetValue<int?>("RateLimiting:Health:PermitLimit") ?? 30;
        int healthWindowSeconds = configuration.GetValue<int?>("RateLimiting:Health:WindowInSeconds") ?? 60;
        int healthConcurrencyLimit = configuration.GetValue<int?>("RateLimiting:Health:ConcurrencyLimit") ?? 4;

        ValidatePositive(globalPermitLimit, "RateLimiting:Global:PermitLimit");
        ValidatePositive(globalWindowSeconds, "RateLimiting:Global:WindowInSeconds");
        ValidatePositive(authPermitLimit, "RateLimiting:Authentication:PermitLimit");
        ValidatePositive(authWindowSeconds, "RateLimiting:Authentication:WindowInSeconds");
        ValidatePositive(authConcurrencyLimit, "RateLimiting:Authentication:ConcurrencyLimit");
        ValidatePositive(authGlobalConcurrencyLimit, "RateLimiting:Authentication:GlobalConcurrencyLimit");
        ValidatePositive(reportingConcurrencyLimit, "RateLimiting:Concurrency:Reporting");
        ValidatePositive(uploadConcurrencyLimit, "RateLimiting:Concurrency:Upload");
        ValidatePositive(postingConcurrencyLimit, "RateLimiting:Concurrency:Posting");
        ValidatePositive(healthPermitLimit, "RateLimiting:Health:PermitLimit");
        ValidatePositive(healthWindowSeconds, "RateLimiting:Health:WindowInSeconds");
        ValidatePositive(healthConcurrencyLimit, "RateLimiting:Health:ConcurrencyLimit");

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                return ValueTask.CompletedTask;
            };

            // A global fixed-window limiter, partitioned by authenticated user or client IP.
            var requestRateLimiter =
                PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    UsesHealthPolicy(httpContext)
                        ? RateLimitPartition.GetNoLimiter("health-probes")
                        : RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: GetPartitionKey(httpContext),
                            factory: _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = globalPermitLimit,
                                Window = TimeSpan.FromSeconds(globalWindowSeconds),
                                QueueLimit = 0,
                                AutoReplenishment = true
                            }));

            var authenticationConcurrencyLimiter =
                PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    UsesAuthenticationPolicy(httpContext)
                        ? RateLimitPartition.GetConcurrencyLimiter(
                            partitionKey: "authentication-global",
                            factory: _ => new ConcurrencyLimiterOptions
                            {
                                PermitLimit = authGlobalConcurrencyLimit,
                                QueueLimit = 0
                            })
                        : RateLimitPartition.GetNoLimiter("non-authentication"));

            var healthConcurrencyLimiter =
                PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    UsesHealthPolicy(httpContext)
                        ? RateLimitPartition.GetConcurrencyLimiter(
                            partitionKey: "health-process",
                            factory: _ => new ConcurrencyLimiterOptions
                            {
                                PermitLimit = healthConcurrencyLimit,
                                QueueLimit = 0
                            })
                        : RateLimitPartition.GetNoLimiter("non-health"));

            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                requestRateLimiter,
                authenticationConcurrencyLimiter,
                healthConcurrencyLimiter);

            // Authentication needs both a request-rate ceiling and a concurrency ceiling because
            // password verification deliberately consumes significant CPU.
            options.AddPolicy(RateLimitingPolicies.Authentication, httpContext =>
                RateLimitPartition.Get(
                    partitionKey: GetPartitionKey(httpContext),
                    factory: _ => RateLimiter.CreateChained(
                        new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = authPermitLimit,
                            Window = TimeSpan.FromSeconds(authWindowSeconds),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }),
                        new ConcurrencyLimiter(new ConcurrencyLimiterOptions
                        {
                            PermitLimit = authConcurrencyLimit,
                            QueueLimit = 0
                        }))));

            // The request budget is per trusted caller so one public client cannot spend the
            // probe allowance for another. The separate global chain above caps concurrent work.
            options.AddPolicy(RateLimitingPolicies.Health, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetPartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = healthPermitLimit,
                        Window = TimeSpan.FromSeconds(healthWindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            AddConcurrencyPolicy(options, RateLimitingPolicies.Reporting, reportingConcurrencyLimit);
            AddConcurrencyPolicy(options, RateLimitingPolicies.Upload, uploadConcurrencyLimit);
            AddConcurrencyPolicy(options, RateLimitingPolicies.Posting, postingConcurrencyLimit);
        });

        return services;
    }

    private static void AddConcurrencyPolicy(
        RateLimiterOptions options,
        string policyName,
        int permitLimit)
    {
        // The constant partition deliberately caps total in-flight work per application instance.
        // A gateway or distributed limiter is still required for a deployment-wide ceiling.
        options.AddPolicy(policyName, _ =>
            RateLimitPartition.GetConcurrencyLimiter(
                partitionKey: policyName,
                factory: _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = permitLimit,
                    QueueLimit = 0
                }));
    }

    private static bool UsesAuthenticationPolicy(HttpContext httpContext) =>
        string.Equals(
            httpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName,
            RateLimitingPolicies.Authentication,
            StringComparison.Ordinal);

    private static bool UsesHealthPolicy(HttpContext httpContext) =>
        string.Equals(
            httpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName,
            RateLimitingPolicies.Health,
            StringComparison.Ordinal);

    internal static string GetPartitionKey(HttpContext httpContext)
    {
        string? userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                         httpContext.User.FindFirstValue("sub");

        return Guid.TryParse(userId, out Guid parsedUserId)
            ? $"user:{parsedUserId:D}"
            : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }

    private static void ValidatePositive(int value, string configurationKey)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException($"{configurationKey} must be greater than zero.");
        }
    }
}

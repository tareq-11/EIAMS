namespace Web.Api.Extensions;

internal static class CorsExtensions
{
    internal const string PolicyName = "DefaultCorsPolicy";

    internal static IServiceCollection AddCorsPolicy(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        string[] allowedOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, builder =>
            {
                if (allowedOrigins.Length == 0 && environment.IsDevelopment())
                {
                    // Development-friendly: allow any origin when no origins are configured.
                    builder
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                }
                else if (allowedOrigins.Length > 0)
                {
                    builder
                        .WithOrigins(allowedOrigins)
                        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                        .WithHeaders("Authorization", "Content-Type", "Idempotency-Key", "Correlation-Id")
                        .WithExposedHeaders("X-Request-Id")
                        .AllowCredentials();
                }
                else
                {
                    // Production is fail-closed when no browser origins are configured.
                    builder
                        .SetIsOriginAllowed(_ => false)
                        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                        .WithHeaders("Authorization", "Content-Type", "Idempotency-Key", "Correlation-Id");
                }
            });
        });

        return services;
    }
}

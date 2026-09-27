namespace Web.Api.Extensions;

internal static class CorsExtensions
{
    internal const string PolicyName = "DefaultCorsPolicy";

    internal static IServiceCollection AddCorsPolicy(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, builder =>
            {
                // Browser clients use the UI origin and reach this API through its same-origin
                // /api/v1 proxy. Cross-origin browser access is intentionally not supported.
                builder.SetIsOriginAllowed(_ => false);
            });
        });

        return services;
    }
}

namespace Web.Api.Extensions;

internal static class SecurityConfigurationExtensions
{
    internal static void ValidateProductionSecurityConfiguration(
        this IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            return;
        }

        string? allowedHosts = configuration["AllowedHosts"];

        if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts.Trim() == "*")
        {
            throw new InvalidOperationException(
                "AllowedHosts must contain explicit production host names; wildcard hosting is not allowed outside Development or Testing.");
        }
    }
}

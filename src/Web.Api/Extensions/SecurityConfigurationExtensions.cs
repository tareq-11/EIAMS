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

        if (string.IsNullOrWhiteSpace(allowedHosts))
        {
            throw new InvalidOperationException(
                "AllowedHosts must contain explicit production host names; wildcard hosting is not allowed outside Development or Testing.");
        }

        string[] hosts = allowedHosts.Split(';', StringSplitOptions.TrimEntries);

        if (hosts.Length == 0 || hosts.Any(host => !IsExplicitHost(host)))
        {
            throw new InvalidOperationException(
                "Every AllowedHosts entry must be an explicit, valid production host name. Wildcards, schemes, paths, and empty entries are not allowed.");
        }
    }

    private static bool IsExplicitHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host) ||
            host.Contains('*', StringComparison.Ordinal) ||
            host.Equals("0.0.0.0", StringComparison.Ordinal) ||
            host.Equals("[::]", StringComparison.Ordinal) ||
            host.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }

        return Uri.TryCreate($"https://{host}", UriKind.Absolute, out Uri? uri) &&
               !string.IsNullOrWhiteSpace(uri.Host) &&
               string.IsNullOrEmpty(uri.UserInfo) &&
               uri.AbsolutePath == "/" &&
               string.IsNullOrEmpty(uri.Query) &&
               string.IsNullOrEmpty(uri.Fragment);
    }
}

using System.Text;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Authentication;

internal sealed class JwtOptions
{
    internal const string SectionName = "Jwt";
    internal const int MinimumSecretBytes = 32;

    public string Secret { get; init; } = string.Empty;

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public int ExpirationInMinutes { get; init; }

    public bool RequireHttpsMetadata { get; init; } = true;

    internal static JwtOptions FromConfiguration(IConfiguration configuration)
    {
        JwtOptions options = configuration
            .GetSection(SectionName)
            .Get<JwtOptions>() ?? new JwtOptions();

        if (Encoding.UTF8.GetByteCount(options.Secret) < MinimumSecretBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:Secret must contain at least {MinimumSecretBytes} bytes and must be supplied by a secure configuration provider.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            throw new InvalidOperationException("Jwt:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            throw new InvalidOperationException("Jwt:Audience is required.");
        }

        if (options.ExpirationInMinutes is < 1 or > 1440)
        {
            throw new InvalidOperationException("Jwt:ExpirationInMinutes must be between 1 and 1440.");
        }

        return options;
    }
}

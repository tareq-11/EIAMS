using System.Text;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Authentication;

internal sealed class JwtOptions
{
    internal const string SectionName = "Jwt";
    internal const int MinimumSecretBytes = 32;

    public string Secret { get; init; } = string.Empty;

    public string ActiveKeyId { get; init; } = string.Empty;

    public Dictionary<string, string> Keys { get; init; } = new(StringComparer.Ordinal);

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public int ExpirationInMinutes { get; init; }

    public bool RequireHttpsMetadata { get; init; } = true;

    internal static JwtOptions FromConfiguration(IConfiguration configuration)
    {
        JwtOptions options = configuration
            .GetSection(SectionName)
            .Get<JwtOptions>() ?? new JwtOptions();

        if (options.Keys.Count == 0)
        {
            if (Encoding.UTF8.GetByteCount(options.Secret) < MinimumSecretBytes)
            {
                throw new InvalidOperationException(
                    $"Jwt:Secret must contain at least {MinimumSecretBytes} bytes and must be supplied by a secure configuration provider.");
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(options.Secret) &&
                Encoding.UTF8.GetByteCount(options.Secret) < MinimumSecretBytes)
            {
                throw new InvalidOperationException(
                    $"Jwt:Secret must contain at least {MinimumSecretBytes} bytes when retained temporarily to validate legacy tokens during key-ring migration.");
            }

            if (options.Keys.Count > 5)
            {
                throw new InvalidOperationException("Jwt:Keys cannot contain more than five overlapping validation keys.");
            }

            if (string.IsNullOrWhiteSpace(options.ActiveKeyId) ||
                !options.Keys.ContainsKey(options.ActiveKeyId))
            {
                throw new InvalidOperationException("Jwt:ActiveKeyId must identify one configured Jwt:Keys entry.");
            }

            foreach ((string keyId, string secret) in options.Keys)
            {
                if (string.IsNullOrWhiteSpace(keyId) || keyId.Length > 64 ||
                    Encoding.UTF8.GetByteCount(secret) < MinimumSecretBytes)
                {
                    throw new InvalidOperationException(
                        $"Every JWT key id must be 1-64 characters and every key must contain at least {MinimumSecretBytes} bytes.");
                }
            }
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

    internal string GetActiveKeyId() => Keys.Count == 0 ? "legacy" : ActiveKeyId;

    internal string GetActiveSecret() => Keys.Count == 0 ? Secret : Keys[ActiveKeyId];

    internal IReadOnlyList<string> GetValidationSecrets(string? keyId)
    {
        if (Keys.Count == 0)
        {
            return string.IsNullOrEmpty(keyId) || string.Equals(keyId, "legacy", StringComparison.Ordinal)
                ? [Secret]
                : [];
        }

        if (keyId is not null && Keys.TryGetValue(keyId, out string? secret))
        {
            return [secret];
        }

        // During migration from the legacy single secret, retain Jwt:Secret only for the short
        // access-token overlap window. It is never used to sign once Jwt:Keys is configured.
        return !string.IsNullOrEmpty(Secret) &&
               (string.IsNullOrEmpty(keyId) || string.Equals(keyId, "legacy", StringComparison.Ordinal))
            ? [Secret]
            : [];
    }
}

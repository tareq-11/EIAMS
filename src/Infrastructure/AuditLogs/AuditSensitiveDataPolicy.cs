using System.Text.Json;

namespace Infrastructure.AuditLogs;

/// <summary>
/// Shared deny-by-name policy used at both audit capture and audit projection boundaries. The
/// write boundary prevents new secrets from being persisted; the read boundary protects legacy or
/// manually imported rows that predate that rule.
/// </summary>
internal static class AuditSensitiveDataPolicy
{
    private static readonly string[] CredentialMarkers =
    [
        "PASSWORD",
        "TOKEN",
        "SECRET",
        "CREDENTIAL",
        "APIKEY",
        "PRIVATEKEY"
    ];

    private static readonly string[] SystemValueMarkers =
    [
        "CONNECTIONSTRING",
        "STORAGEKEY"
    ];

    public static bool IsSensitive(string fieldName) => GetReason(fieldName) is not null;

    public static bool ContainsSensitiveJsonProperty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return ContainsSensitiveProperty(document.RootElement);
        }
        catch (JsonException)
        {
            // Invalid historical summaries are safer to withhold than to return verbatim.
            return true;
        }
    }

    public static string? GetReason(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return null;
        }

        string normalized = string.Concat(fieldName.Where(char.IsLetterOrDigit)).ToUpperInvariant();

        if (SystemValueMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal)))
        {
            return "SENSITIVE_SYSTEM_VALUE";
        }

        return CredentialMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal))
            ? "CONFIDENTIAL_CREDENTIAL"
            : null;
    }

    private static bool ContainsSensitiveProperty(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (IsSensitive(property.Name) || ContainsSensitiveProperty(property.Value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (ContainsSensitiveProperty(item))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

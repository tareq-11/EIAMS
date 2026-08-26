using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Application.Abstractions.Audit;

namespace Infrastructure.AuditLogs;

/// <summary>
/// The write-time value policy for audit capture. Field exclusions keep audit noise (ids, stamps,
/// row versions) out of the entries; forbidden fields guarantee secrets never reach the log; the
/// serializer produces deterministic, culture-invariant strings and replaces oversized values with
/// SHA-256 markers instead of truncating them.
/// </summary>
internal sealed class AuditValuePolicy : IAuditValuePolicy
{
    private const int MaxOriginalFilenameBytes = 1024;
    private const int MaxStringValueBytes = 16384;
    private const string MarkerPrefix = "sha256:";

    private static readonly HashSet<string> ExcludedFields = new(StringComparer.Ordinal)
    {
        "id",
        "created_at_utc",
        "created_by",
        "updated_at_utc",
        "updated_by",
        "row_version"
    };

    private static readonly HashSet<(string EntityType, string Field)> ForbiddenFields =
        new HashSet<(string, string)>
        {
            ("User", "password_hash"),
            ("RefreshToken", "token"),
            ("DocumentAttachment", "storage_key")
        };

    public bool IsExcludedField(string canonicalEntityType, string storeFieldName) =>
        ExcludedFields.Contains(storeFieldName);

    public bool IsForbidden(string canonicalEntityType, string storeFieldName) =>
        ForbiddenFields.Contains((canonicalEntityType, storeFieldName));

    public string? Serialize(string entityType, string field, object? value)
    {
        if (value is null || value is byte[])
        {
            return null;
        }

        return value switch
        {
            Guid guid => guid.ToString("D"),
            Enum enumeration => enumeration.ToString(),
            string text => SerializeText(entityType, field, text),
            DateTime timestamp => FormatTimestamp(timestamp),
            DateTimeOffset offset => offset.ToString("O", CultureInfo.InvariantCulture),
            bool flag => flag.ToString(CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => SerializeText(entityType, field, value.ToString() ?? string.Empty)
        };
    }

    private static string SerializeText(string entityType, string field, string text)
    {
        int byteCount = Encoding.UTF8.GetByteCount(text);

        if (IsOriginalFilename(entityType, field))
        {
            return byteCount <= MaxOriginalFilenameBytes
                ? text
                : $"{MarkerPrefix}{ComputeSha256Hex(text)}";
        }

        return byteCount <= MaxStringValueBytes
            ? text
            : $"{MarkerPrefix}{ComputeSha256Hex(text)} (len:{byteCount})";
    }

    private static bool IsOriginalFilename(string entityType, string field) =>
        entityType == "DocumentAttachment" &&
        field == "original_filename";

    private static string FormatTimestamp(DateTime timestamp)
    {
        DateTime utc = timestamp.Kind switch
        {
            DateTimeKind.Utc => timestamp,
            DateTimeKind.Local => timestamp.ToUniversalTime(),
            _ => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
        };

        return utc.ToString("O", CultureInfo.InvariantCulture);
    }

    private static string ComputeSha256Hex(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

namespace Application.Abstractions.Audit;

/// <summary>
/// Central write-time policy for audit capture: which fields are noise, which fields hold secrets
/// that must never reach the audit log, and how scalar property values are serialized into
/// deterministic, bounded strings for <c>AuditLogEntry</c> old/new values.
/// </summary>
public interface IAuditValuePolicy
{
    /// <summary>
    /// Returns <see langword="true"/> when the field is capture noise (ids, audit stamps,
    /// concurrency tokens) and must not produce field-level entries.
    /// </summary>
    bool IsExcludedField(string canonicalEntityType, string storeFieldName);

    /// <summary>
    /// Returns <see langword="true"/> when the field holds secret material (password hashes,
    /// bearer tokens, storage keys) that must never be persisted in any form.
    /// </summary>
    bool IsForbidden(string canonicalEntityType, string storeFieldName);

    /// <summary>
    /// Serializes a scalar property value into its deterministic audit string representation.
    /// Returns <see langword="null"/> when the value must not be persisted (e.g. binary content).
    /// Never truncates: oversized strings are replaced by a SHA-256 marker.
    /// </summary>
    string? Serialize(string entityType, string field, object? value);
}

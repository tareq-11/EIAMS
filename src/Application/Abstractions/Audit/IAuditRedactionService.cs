using Application.AuditLogs;

namespace Application.Abstractions.Audit;

/// <summary>
/// Service at the read projection boundary responsible for redacting sensitive fields
/// (e.g. passwords, tokens, API keys, connection strings) and enriching audit records
/// with bilingual (Arabic and English) display labels.
/// </summary>
public interface IAuditRedactionService
{
    /// <summary>
    /// Checks if a given field is sensitive and must be masked at the read boundary.
    /// </summary>
    bool IsRedactedField(string entityType, string fieldName);

    /// <summary>
    /// Returns the reason code for why a field is redacted.
    /// </summary>
    string? GetRedactionReason(string entityType, string fieldName);

    /// <summary>
    /// Masks sensitive field values and populates redaction metadata and display labels.
    /// </summary>
    AuditLogEntryResponse RedactEntry(string entityType, AuditLogEntryResponse entry);

    /// <summary>
    /// Masks a collection of entries for a given entity type.
    /// </summary>
    IReadOnlyList<AuditLogEntryResponse> RedactEntries(string entityType, IReadOnlyList<AuditLogEntryResponse> entries);

    /// <summary>
    /// Detects whether a legacy JSON summary contains a sensitive property and must not be returned.
    /// </summary>
    bool IsSummaryRedacted(string? summary);

    /// <summary>
    /// Returns Arabic display label for an audit action.
    /// </summary>
    string GetActionDisplayAr(string action);

    /// <summary>
    /// Returns English display label for an audit action.
    /// </summary>
    string GetActionDisplayEn(string action);

    /// <summary>
    /// Returns Arabic display label for an entity type.
    /// </summary>
    string GetEntityTypeDisplayAr(string entityType);

    /// <summary>
    /// Returns English display label for an entity type.
    /// </summary>
    string GetEntityTypeDisplayEn(string entityType);

    /// <summary>
    /// Returns Arabic display label for a field name.
    /// </summary>
    string GetFieldDisplayAr(string entityType, string fieldName);

    /// <summary>
    /// Returns English display label for a field name.
    /// </summary>
    string GetFieldDisplayEn(string entityType, string fieldName);
}

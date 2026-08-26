using System.Text.RegularExpressions;
using SharedKernel;

namespace Domain.AuditLogs;

/// <summary>
/// One field-level change belonging to an <see cref="AuditLog"/>. Immutable and append-only like
/// its parent; at least one of old/new must be present and they must differ.
/// </summary>
public sealed partial class AuditLogEntry : Entity
{
    public const int MaxFieldNameLength = 100;

    [GeneratedRegex("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex FieldNamePattern();

    private AuditLogEntry() { }

    public Guid AuditLogId { get; private set; }

    public string FieldName { get; private set; }

    public string? OldValue { get; private set; }

    public string? NewValue { get; private set; }

    public static Result<AuditLogEntry> Create(
        Guid id,
        Guid auditLogId,
        string fieldName,
        string? oldValue,
        string? newValue)
    {
        if (id == Guid.Empty || auditLogId == Guid.Empty)
        {
            return Result.Failure<AuditLogEntry>(AuditLogEntryErrors.IdentityRequired);
        }

        bool isFieldNameValid = !string.IsNullOrWhiteSpace(fieldName) &&
            fieldName.Length <= MaxFieldNameLength &&
            FieldNamePattern().IsMatch(fieldName);

        if (!isFieldNameValid)
        {
            return Result.Failure<AuditLogEntry>(AuditLogEntryErrors.FieldNameInvalid);
        }

        if (oldValue is null && newValue is null)
        {
            return Result.Failure<AuditLogEntry>(AuditLogEntryErrors.FieldChangeRequired);
        }

        if (oldValue is not null && newValue is not null && string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            return Result.Failure<AuditLogEntry>(AuditLogEntryErrors.NoChangePair);
        }

        var entry = new AuditLogEntry
        {
            Id = id,
            AuditLogId = auditLogId,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue
        };

        return entry;
    }
}

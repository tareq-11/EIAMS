using SharedKernel;

namespace Domain.AuditLogs;

public static class AuditLogEntryErrors
{
    public static readonly Error IdentityRequired = Error.Problem("AuditLogs.AuditLogEntryIdentityRequired", "Audit log entry id and audit log id values are required.");
    public static readonly Error FieldNameInvalid = Error.Problem("AuditLogs.FieldNameInvalid", "Field name must be non-blank lowercase snake_case of at most 100 characters.");
    public static readonly Error FieldChangeRequired = Error.Problem("AuditLogs.FieldChangeRequired", "At least one of old value or new value is required.");
    public static readonly Error NoChangePair = Error.Conflict("AuditLogs.NoChangePair", "Old value and new value must differ when both are provided.");
}

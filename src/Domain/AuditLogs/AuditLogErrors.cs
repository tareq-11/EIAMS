using SharedKernel;

namespace Domain.AuditLogs;

public static class AuditLogErrors
{
    public static Error NotFound(Guid auditLogId) => Error.NotFound(
        "AuditLogs.NotFound",
        "Audit log was not found.",
        new { audit_log_id = auditLogId });

    public static readonly Error FilterInvalid = Error.Problem(
        "AuditLogs.FilterInvalid",
        "One or more audit log filters are invalid.");

    public static readonly Error Unauthorized = Error.Forbidden(
        "AuditLogs.Unauthorized",
        "You do not have permission to view audit logs.");

    public static readonly Error NoScopeAssigned = Error.Forbidden(
        "AuditLogs.NoScopeAssigned",
        "User has no active authorization scope.");

    public static readonly Error OperationIdRequired = Error.Problem("AuditLogs.OperationIdRequired", "Audit log id and operation id values are required.");
    public static readonly Error EntityIdRequired = Error.Problem("AuditLogs.EntityIdRequired", "A non-empty entity id is required.");
    public static readonly Error EntityTypeInvalid = Error.Problem("AuditLogs.EntityTypeInvalid", "Entity type must be a non-blank canonical name of at most 100 characters.");
    public static readonly Error AggregateTypeInvalid = Error.Problem("AuditLogs.AggregateTypeInvalid", "Aggregate type must be a non-blank canonical name of at most 100 characters when provided.");
    public static readonly Error UserIdInvalid = Error.Problem("AuditLogs.UserIdInvalid", "User id must be a known non-empty value when provided.");
    public static readonly Error ActionInvalid = Error.Problem("AuditLogs.ActionInvalid", "Action must be a non-blank member of the audit action vocabulary of at most 50 characters.");
    public static readonly Error RequestIdTooLong = Error.Problem("AuditLogs.RequestIdTooLong", "Request id must not exceed 64 characters.", new { max_length = 64 });
    public static readonly Error CommandNameTooLong = Error.Problem("AuditLogs.CommandNameTooLong", "Command name must not exceed 150 characters.", new { max_length = 150 });
    public static readonly Error SummaryNotJsonObject = Error.Problem("AuditLogs.SummaryNotJsonObject", "Summary must be a JSON object or null.");
    public static readonly Error SummaryTooLarge = Error.Problem("AuditLogs.SummaryTooLarge", "Summary must not exceed 16384 UTF-8 bytes.", new { max_bytes = 16384 });
    public static readonly Error IpAddressTooLong = Error.Problem("AuditLogs.IpAddressTooLong", "IP address must not exceed 45 characters.", new { max_length = 45 });
    public static readonly Error TimestampNotUtc = Error.Problem("AuditLogs.TimestampNotUtc", "Created at timestamp must be expressed in UTC.");
}

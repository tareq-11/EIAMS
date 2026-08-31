namespace Application.AuditLogs;

public sealed record AuditLogListItemResponse(
    Guid Id,
    Guid OperationId,
    string? RequestId,
    Guid? UserId,
    string EntityType,
    Guid EntityId,
    string? AggregateType,
    Guid? AggregateId,
    string Action,
    string? CommandName,
    DateTime CreatedAtUtc,
    string? ActionDisplayAr = null,
    string? ActionDisplayEn = null,
    string? EntityTypeDisplayAr = null,
    string? EntityTypeDisplayEn = null);

public sealed record AuditLogEntryResponse(
    Guid Id,
    string FieldName,
    string? OldValue,
    string? NewValue,
    bool IsRedacted = false,
    string? RedactionReason = null,
    string? FieldDisplayAr = null,
    string? FieldDisplayEn = null);

public sealed record AuditLogDetailsResponse(
    Guid Id,
    Guid OperationId,
    string? RequestId,
    Guid? UserId,
    string EntityType,
    Guid EntityId,
    string? AggregateType,
    Guid? AggregateId,
    string Action,
    string? CommandName,
    string? Summary,
    bool IsSummaryRedacted,
    string? SummaryRedactionReason,
    string? IpAddress,
    DateTime CreatedAtUtc,
    IReadOnlyList<AuditLogEntryResponse> Entries,
    string? ActionDisplayAr = null,
    string? ActionDisplayEn = null,
    string? EntityTypeDisplayAr = null,
    string? EntityTypeDisplayEn = null);

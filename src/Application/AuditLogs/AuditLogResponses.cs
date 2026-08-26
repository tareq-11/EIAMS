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
    DateTime CreatedAtUtc);

public sealed record AuditLogEntryResponse(
    Guid Id,
    string FieldName,
    string? OldValue,
    string? NewValue);

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
    string? IpAddress,
    DateTime CreatedAtUtc,
    IReadOnlyList<AuditLogEntryResponse> Entries);

using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.AuditLogs.GetByField;

public sealed record GetAuditLogsByFieldQuery(
    string FieldName,
    string? EntityType,
    Guid? EntityId,
    Guid? UserId,
    string? Action,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int Page,
    int PageSize) : IQuery<PagedResult<AuditLogListItemResponse>>;

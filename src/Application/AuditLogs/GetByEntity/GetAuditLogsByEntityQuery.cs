using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.AuditLogs.GetByEntity;

public sealed record GetAuditLogsByEntityQuery(
    string EntityType,
    Guid EntityId,
    string? Action,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int Page,
    int PageSize) : IQuery<PagedResult<AuditLogListItemResponse>>;

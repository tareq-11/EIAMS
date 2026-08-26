using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.AuditLogs.GetByUser;

public sealed record GetAuditLogsByUserQuery(
    Guid UserId,
    string? EntityType,
    string? Action,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int Page,
    int PageSize) : IQuery<PagedResult<AuditLogListItemResponse>>;

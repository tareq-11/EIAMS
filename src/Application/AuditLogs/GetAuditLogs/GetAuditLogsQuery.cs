using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.AuditLogs.GetAuditLogs;

public sealed record GetAuditLogsQuery(
    Guid? UserId = null,
    string? EntityType = null,
    Guid? EntityId = null,
    string? Action = null,
    string? FieldName = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string? Search = null,
    int Page = PaginationDefaults.DefaultPage,
    int PageSize = PaginationDefaults.DefaultPageSize)
    : IQuery<PagedResult<AuditLogListItemResponse>>;

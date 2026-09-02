using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.AuditLogs.GetAuditLogs;

public sealed record GetAuditLogsKeysetQuery(
    Guid? UserId = null,
    string? EntityType = null,
    Guid? EntityId = null,
    string? Action = null,
    string? FieldName = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string? Search = null,
    DateTimeOffset? AfterCreatedAtUtc = null,
    Guid? AfterId = null,
    int PageSize = PaginationDefaults.DefaultPageSize)
    : IQuery<KeysetPage<AuditLogListItemResponse>>;

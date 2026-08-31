using Application.Abstractions.Audit;
using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.AuditLogs;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.AuditLogs.GetAuditLogs;

internal sealed class GetAuditLogsQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IAuditRedactionService redactionService)
    : IQueryHandler<GetAuditLogsQuery, PagedResult<AuditLogListItemResponse>>
{
    public async Task<Result<PagedResult<AuditLogListItemResponse>>> Handle(
        GetAuditLogsQuery query,
        CancellationToken cancellationToken)
    {
        if (!AuditLogQuerySupport.AreGlobalFiltersValid(
                query.UserId,
                query.EntityType,
                query.EntityId,
                query.Action,
                query.FieldName,
                query.FromUtc,
                query.ToUtc,
                query.Search,
                query.Page,
                query.PageSize))
        {
            return Result.Failure<PagedResult<AuditLogListItemResponse>>(AuditLogErrors.FilterInvalid);
        }

        IQueryable<AuditLog> source = context.AuditLogs.AsNoTracking();

        Result<IQueryable<AuditLog>> scopeResult = await AuditLogQuerySupport.AuthorizeAndApplyScopeAsync(
            source,
            context,
            scopeAuthorizationService,
            userContext.UserId,
            cancellationToken);

        if (scopeResult.IsFailure)
        {
            return Result.Failure<PagedResult<AuditLogListItemResponse>>(scopeResult.Error);
        }

        source = scopeResult.Value;

        // 2. Query Filters
        if (query.UserId.HasValue)
        {
            source = source.Where(log => log.UserId == query.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            source = source.Where(log => log.EntityType == query.EntityType);
        }

        if (query.EntityId.HasValue)
        {
            source = source.Where(log => log.EntityId == query.EntityId.Value);
        }

        source = AuditLogQuerySupport.ApplyFilters(source, query.Action, query.FromUtc, query.ToUtc);

        if (!string.IsNullOrWhiteSpace(query.FieldName))
        {
            string field = query.FieldName;
            source = source.Where(log => context.AuditLogEntries.Any(entry =>
                entry.AuditLogId == log.Id && entry.FieldName == field));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string term = query.Search.Trim();
            source = source.Where(log =>
                EF.Functions.Like(log.Action, $"%{term}%") ||
                EF.Functions.Like(log.EntityType, $"%{term}%") ||
                log.CommandName != null && EF.Functions.Like(log.CommandName, $"%{term}%") ||
                log.RequestId != null && EF.Functions.Like(log.RequestId, $"%{term}%"));
        }

        return await AuditLogQuerySupport.ToPagedResultAsync(
            source,
            query.Page,
            query.PageSize,
            cancellationToken,
            redactionService);
    }
}

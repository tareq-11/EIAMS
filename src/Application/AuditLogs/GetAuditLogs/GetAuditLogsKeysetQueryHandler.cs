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

internal sealed class GetAuditLogsKeysetQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IAuditRedactionService redactionService)
    : IQueryHandler<GetAuditLogsKeysetQuery, KeysetPage<AuditLogListItemResponse>>
{
    public async Task<Result<KeysetPage<AuditLogListItemResponse>>> Handle(
        GetAuditLogsKeysetQuery query,
        CancellationToken cancellationToken)
    {
        bool cursorIsValid = query.AfterCreatedAtUtc.HasValue == query.AfterId.HasValue;
        if (!cursorIsValid || !AuditLogQuerySupport.AreGlobalFiltersValid(
                query.UserId,
                query.EntityType,
                query.EntityId,
                query.Action,
                query.FieldName,
                query.FromUtc,
                query.ToUtc,
                query.Search,
                PaginationDefaults.DefaultPage,
                query.PageSize))
        {
            return Result.Failure<KeysetPage<AuditLogListItemResponse>>(AuditLogErrors.FilterInvalid);
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
            return Result.Failure<KeysetPage<AuditLogListItemResponse>>(scopeResult.Error);
        }

        source = ApplyFilters(scopeResult.Value, query);

        if (query.AfterCreatedAtUtc.HasValue && query.AfterId.HasValue)
        {
            DateTime afterCreatedAtUtc = query.AfterCreatedAtUtc.Value.UtcDateTime;
            Guid afterId = query.AfterId.Value;
            source = source.Where(item =>
                item.CreatedAtUtc < afterCreatedAtUtc ||
                item.CreatedAtUtc == afterCreatedAtUtc && item.Id.CompareTo(afterId) < 0);
        }

        return await AuditLogQuerySupport.ToKeysetPageAsync(
            source,
            query.PageSize,
            cancellationToken,
            redactionService);
    }

    private IQueryable<AuditLog> ApplyFilters(IQueryable<AuditLog> source, GetAuditLogsKeysetQuery query)
    {
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

        return source;
    }
}

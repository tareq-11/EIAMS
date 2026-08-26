using Application.Abstractions.Pagination;
using Domain.AuditLogs;
using Microsoft.EntityFrameworkCore;

namespace Application.AuditLogs;

internal static class AuditLogQuerySupport
{
    public static bool AreFiltersValid(string? action, DateTimeOffset? fromUtc, DateTimeOffset? toUtc) =>
        (action is null || AuditActions.All.Contains(action)) &&
        (!fromUtc.HasValue || !toUtc.HasValue || fromUtc.Value < toUtc.Value);

    public static IQueryable<AuditLog> ApplyFilters(
        IQueryable<AuditLog> query,
        string? action,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc)
    {
        if (action is not null)
        {
            query = query.Where(item => item.Action == action);
        }

        if (fromUtc.HasValue)
        {
            DateTime from = fromUtc.Value.UtcDateTime;
            query = query.Where(item => item.CreatedAtUtc >= from);
        }

        if (toUtc.HasValue)
        {
            DateTime to = toUtc.Value.UtcDateTime;
            query = query.Where(item => item.CreatedAtUtc < to);
        }

        return query;
    }

    public static async Task<PagedResult<AuditLogListItemResponse>> ToPagedResultAsync(
        IQueryable<AuditLog> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, PaginationDefaults.DefaultPage);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(page, PaginationDefaults.MaximumPage);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, PaginationDefaults.MaximumPageSize);

        int totalItems = await query.CountAsync(cancellationToken);
        int offset = checked((page - 1) * pageSize);

        List<AuditLogListItemResponse> items = await query
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.Id)
            .Skip(offset)
            .Take(pageSize)
            .Select(item => new AuditLogListItemResponse(
            item.Id,
            item.OperationId,
            item.RequestId,
            item.UserId,
            item.EntityType,
            item.EntityId,
            item.AggregateType,
            item.AggregateId,
            item.Action,
            item.CommandName,
            item.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogListItemResponse>(items, page, pageSize, totalItems);
    }
}

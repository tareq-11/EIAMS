using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Application.Abstractions.Pagination;

public static class PaginationExtensions
{
    public static async Task<PagedResult<TResult>> ToPagedResultAsync<TSource, TResult>(
        this IOrderedQueryable<TSource> query,
        Expression<Func<TSource, TResult>> selector,
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

        List<TResult> items = await query
            .Skip(offset)
            .Take(pageSize)
            .Select(selector)
            .ToListAsync(cancellationToken);

        return new PagedResult<TResult>(items, page, pageSize, totalItems);
    }

    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IOrderedQueryable<T> query,
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

        List<T> items = await query
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, page, pageSize, totalItems);
    }
}

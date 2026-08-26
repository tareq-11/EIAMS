using System.Text.RegularExpressions;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.AuditLogs;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.AuditLogs.GetByField;

internal sealed partial class GetAuditLogsByFieldQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetAuditLogsByFieldQuery, PagedResult<AuditLogListItemResponse>>
{
    [GeneratedRegex("^[a-z][a-z0-9_]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex FieldNamePattern();

    public async Task<Result<PagedResult<AuditLogListItemResponse>>> Handle(
        GetAuditLogsByFieldQuery query,
        CancellationToken cancellationToken)
    {
        if (!FieldNamePattern().IsMatch(query.FieldName) ||
            query.EntityType is not null && !KnownAuditEntityTypes.IsKnown(query.EntityType) ||
            query.EntityId == Guid.Empty ||
            query.UserId == Guid.Empty ||
            !AuditLogQuerySupport.AreFiltersValid(query.Action, query.FromUtc, query.ToUtc))
        {
            return Result.Failure<PagedResult<AuditLogListItemResponse>>(AuditLogErrors.FilterInvalid);
        }

        IQueryable<AuditLog> source = context.AuditLogs
            .AsNoTracking()
            .Where(log => context.AuditLogEntries.Any(entry =>
                entry.AuditLogId == log.Id && entry.FieldName == query.FieldName));

        if (query.EntityType is not null)
        {
            source = source.Where(item => item.EntityType == query.EntityType);
        }

        if (query.EntityId.HasValue)
        {
            source = source.Where(item => item.EntityId == query.EntityId.Value);
        }

        if (query.UserId.HasValue)
        {
            source = source.Where(item => item.UserId == query.UserId.Value);
        }

        source = AuditLogQuerySupport.ApplyFilters(source, query.Action, query.FromUtc, query.ToUtc);

        return await AuditLogQuerySupport.ToPagedResultAsync(
            source, query.Page, query.PageSize, cancellationToken);
    }
}

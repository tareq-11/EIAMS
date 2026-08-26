using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.AuditLogs;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.AuditLogs.GetByUser;

internal sealed class GetAuditLogsByUserQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetAuditLogsByUserQuery, PagedResult<AuditLogListItemResponse>>
{
    public async Task<Result<PagedResult<AuditLogListItemResponse>>> Handle(
        GetAuditLogsByUserQuery query,
        CancellationToken cancellationToken)
    {
        if (query.UserId == Guid.Empty ||
            query.EntityType is not null && !KnownAuditEntityTypes.IsKnown(query.EntityType) ||
            !AuditLogQuerySupport.AreFiltersValid(query.Action, query.FromUtc, query.ToUtc))
        {
            return Result.Failure<PagedResult<AuditLogListItemResponse>>(AuditLogErrors.FilterInvalid);
        }

        IQueryable<AuditLog> source = context.AuditLogs
            .AsNoTracking()
            .Where(item => item.UserId == query.UserId);

        if (query.EntityType is not null)
        {
            source = source.Where(item => item.EntityType == query.EntityType);
        }

        source = AuditLogQuerySupport.ApplyFilters(source, query.Action, query.FromUtc, query.ToUtc);

        return await AuditLogQuerySupport.ToPagedResultAsync(
            source, query.Page, query.PageSize, cancellationToken);
    }
}

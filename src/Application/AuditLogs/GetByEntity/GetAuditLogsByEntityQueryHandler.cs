using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.AuditLogs;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.AuditLogs.GetByEntity;

internal sealed class GetAuditLogsByEntityQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetAuditLogsByEntityQuery, PagedResult<AuditLogListItemResponse>>
{
    public async Task<Result<PagedResult<AuditLogListItemResponse>>> Handle(
        GetAuditLogsByEntityQuery query,
        CancellationToken cancellationToken)
    {
        if (!KnownAuditEntityTypes.IsKnown(query.EntityType) ||
            query.EntityId == Guid.Empty ||
            !AuditLogQuerySupport.AreFiltersValid(query.Action, query.FromUtc, query.ToUtc))
        {
            return Result.Failure<PagedResult<AuditLogListItemResponse>>(AuditLogErrors.FilterInvalid);
        }

        IQueryable<AuditLog> source = context.AuditLogs
            .AsNoTracking()
            .Where(item => item.EntityType == query.EntityType && item.EntityId == query.EntityId);

        source = AuditLogQuerySupport.ApplyFilters(source, query.Action, query.FromUtc, query.ToUtc);

        return await AuditLogQuerySupport.ToPagedResultAsync(
            source, query.Page, query.PageSize, cancellationToken);
    }
}

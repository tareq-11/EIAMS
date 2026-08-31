using Application.Abstractions.Audit;
using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.AuditLogs;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.AuditLogs.GetByEntity;

internal sealed class GetAuditLogsByEntityQueryHandler(
    IApplicationDbContext context,
    IAuditRedactionService redactionService,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetAuditLogsByEntityQuery, PagedResult<AuditLogListItemResponse>>
{
    public async Task<Result<PagedResult<AuditLogListItemResponse>>> Handle(
        GetAuditLogsByEntityQuery query,
        CancellationToken cancellationToken)
    {
        if (!KnownAuditEntityTypes.IsKnown(query.EntityType) ||
            query.EntityId == Guid.Empty ||
            !AuditLogQuerySupport.AreFiltersValid(query.Action, query.FromUtc, query.ToUtc) ||
            !AuditLogQuerySupport.ArePaginationParametersValid(query.Page, query.PageSize))
        {
            return Result.Failure<PagedResult<AuditLogListItemResponse>>(AuditLogErrors.FilterInvalid);
        }

        IQueryable<AuditLog> source = context.AuditLogs
            .AsNoTracking()
            .Where(item => item.EntityType == query.EntityType && item.EntityId == query.EntityId);

        source = AuditLogQuerySupport.ApplyFilters(source, query.Action, query.FromUtc, query.ToUtc);

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

        return await AuditLogQuerySupport.ToPagedResultAsync(
            scopeResult.Value, query.Page, query.PageSize, cancellationToken, redactionService);
    }
}

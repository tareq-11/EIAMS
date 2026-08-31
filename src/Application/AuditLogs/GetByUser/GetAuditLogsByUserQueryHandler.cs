using Application.Abstractions.Audit;
using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.AuditLogs;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.AuditLogs.GetByUser;

internal sealed class GetAuditLogsByUserQueryHandler(
    IApplicationDbContext context,
    IAuditRedactionService redactionService,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetAuditLogsByUserQuery, PagedResult<AuditLogListItemResponse>>
{
    public async Task<Result<PagedResult<AuditLogListItemResponse>>> Handle(
        GetAuditLogsByUserQuery query,
        CancellationToken cancellationToken)
    {
        if (query.UserId == Guid.Empty ||
            query.EntityType is not null && !KnownAuditEntityTypes.IsKnown(query.EntityType) ||
            !AuditLogQuerySupport.AreFiltersValid(query.Action, query.FromUtc, query.ToUtc) ||
            !AuditLogQuerySupport.ArePaginationParametersValid(query.Page, query.PageSize))
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

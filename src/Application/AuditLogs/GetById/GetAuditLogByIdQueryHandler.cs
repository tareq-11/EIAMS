using Application.Abstractions.Audit;
using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.AuditLogs;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.AuditLogs.GetById;

internal sealed class GetAuditLogByIdQueryHandler(
    IApplicationDbContext context,
    IAuditRedactionService redactionService,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetAuditLogByIdQuery, AuditLogDetailsResponse>
{
    public async Task<Result<AuditLogDetailsResponse>> Handle(
        GetAuditLogByIdQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<AuditLog> source = context.AuditLogs
            .AsNoTracking()
            .Where(item => item.Id == query.AuditLogId);

        Result<IQueryable<AuditLog>> scopeResult = await AuditLogQuerySupport.AuthorizeAndApplyScopeAsync(
            source,
            context,
            scopeAuthorizationService,
            userContext.UserId,
            cancellationToken);

        if (scopeResult.IsFailure)
        {
            return Result.Failure<AuditLogDetailsResponse>(scopeResult.Error);
        }

        AuditLog? log = await scopeResult.Value.SingleOrDefaultAsync(cancellationToken);

        if (log is null)
        {
            return Result.Failure<AuditLogDetailsResponse>(AuditLogErrors.NotFound(query.AuditLogId));
        }

        List<AuditLogEntryResponse> rawEntries = await context.AuditLogEntries
            .AsNoTracking()
            .Where(item => item.AuditLogId == query.AuditLogId)
            .OrderBy(item => item.FieldName)
            .ThenBy(item => item.Id)
            .Select(item => new AuditLogEntryResponse(
                item.Id,
                item.FieldName,
                item.OldValue,
                item.NewValue,
                false,
                null,
                null,
                null))
            .ToListAsync(cancellationToken);

        IReadOnlyList<AuditLogEntryResponse> redactedEntries =
            redactionService.RedactEntries(log.EntityType, rawEntries);
        bool isSummaryRedacted = redactionService.IsSummaryRedacted(log.Summary);

        var details = new AuditLogDetailsResponse(
            log.Id,
            log.OperationId,
            log.RequestId,
            log.UserId,
            log.EntityType,
            log.EntityId,
            log.AggregateType,
            log.AggregateId,
            log.Action,
            log.CommandName,
            isSummaryRedacted ? null : log.Summary,
            isSummaryRedacted,
            isSummaryRedacted ? "SENSITIVE_SUMMARY" : null,
            log.IpAddress,
            log.CreatedAtUtc,
            redactedEntries,
            redactionService.GetActionDisplayAr(log.Action),
            redactionService.GetActionDisplayEn(log.Action),
            redactionService.GetEntityTypeDisplayAr(log.EntityType),
            redactionService.GetEntityTypeDisplayEn(log.EntityType));

        return details;
    }
}

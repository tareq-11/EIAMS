using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.AuditLogs;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.AuditLogs.GetById;

internal sealed class GetAuditLogByIdQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetAuditLogByIdQuery, AuditLogDetailsResponse>
{
    public async Task<Result<AuditLogDetailsResponse>> Handle(
        GetAuditLogByIdQuery query,
        CancellationToken cancellationToken)
    {
        AuditLogDetailsResponse? header = await context.AuditLogs
            .AsNoTracking()
            .Where(item => item.Id == query.AuditLogId)
            .Select(item => new AuditLogDetailsResponse(
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
                item.Summary,
                item.IpAddress,
                item.CreatedAtUtc,
                Array.Empty<AuditLogEntryResponse>()))
            .SingleOrDefaultAsync(cancellationToken);

        if (header is null)
        {
            return Result.Failure<AuditLogDetailsResponse>(AuditLogErrors.NotFound(query.AuditLogId));
        }

        List<AuditLogEntryResponse> entries = await context.AuditLogEntries
            .AsNoTracking()
            .Where(item => item.AuditLogId == query.AuditLogId)
            .OrderBy(item => item.FieldName)
            .ThenBy(item => item.Id)
            .Select(item => new AuditLogEntryResponse(
                item.Id,
                item.FieldName,
                item.OldValue,
                item.NewValue))
            .ToListAsync(cancellationToken);

        return header with { Entries = entries };
    }
}

using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.WarehouseDocuments.GetHistory;

internal sealed class GetWarehouseDocumentHistoryQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetWarehouseDocumentHistoryQuery, WarehouseDocumentHistoryResponse>
{
    public async Task<Result<WarehouseDocumentHistoryResponse>> Handle(
        GetWarehouseDocumentHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var document = await context.WarehouseDocuments
            .AsNoTracking()
            .Where(item => item.Id == query.DocumentId)
            .Select(item => new { item.WarehouseId, item.DocumentStatus, item.RowVersion })
            .SingleOrDefaultAsync(cancellationToken);

        if (document is null)
        {
            return Result.Failure<WarehouseDocumentHistoryResponse>(WarehouseDocumentErrors.NotFound(query.DocumentId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.View,
            ScopeType.Warehouse,
            document.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<WarehouseDocumentHistoryResponse>(WarehouseDocumentErrors.NotFound(query.DocumentId));
        }

        List<DocumentLifecycleEventResponse> events = await context.DocumentLifecycleEvents
            .AsNoTracking()
            .Where(item => item.DocumentId == query.DocumentId)
            .OrderBy(item => item.OccurredAtUtc)
            .ThenBy(item => item.Id)
            .Select(item => new DocumentLifecycleEventResponse(
                item.Id,
                item.FromStatus == null ? null : item.FromStatus.ToString(),
                item.ToStatus.ToString(),
                item.Action,
                item.Reason,
                item.ActorUserId,
                item.ActorDisplayName,
                item.OccurredAtUtc,
                item.ResultingRowVersion,
                item.RequestId,
                item.OperationId))
            .ToListAsync(cancellationToken);

        return new WarehouseDocumentHistoryResponse(
            query.DocumentId,
            document.DocumentStatus.ToString(),
            document.RowVersion,
            events);
    }
}

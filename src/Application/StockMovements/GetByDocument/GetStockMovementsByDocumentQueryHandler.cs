using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.StockMovements.GetByDocument;

internal sealed class GetStockMovementsByDocumentQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetStockMovementsByDocumentQuery, PagedResult<StockMovementResponse>>
{
    public async Task<Result<PagedResult<StockMovementResponse>>> Handle(
        GetStockMovementsByDocumentQuery query,
        CancellationToken cancellationToken)
    {
        WarehouseDocument? document = await context.WarehouseDocuments
            .SingleOrDefaultAsync(d => d.Id == query.DocumentId, cancellationToken);

        if (document is null)
        {
            return Result.Failure<PagedResult<StockMovementResponse>>(WarehouseDocumentErrors.NotFound(query.DocumentId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.View,
            ScopeType.Warehouse,
            document.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<PagedResult<StockMovementResponse>>(
                WarehouseDocumentErrors.NotFound(query.DocumentId));
        }

        PagedResult<StockMovementResponse> movements = await context.StockMovements
            .Where(m => m.DocumentId == query.DocumentId)
            .Select(m => new StockMovementResponse
            {
                Id = m.Id,
                WarehouseId = m.WarehouseId,
                MaterialId = m.MaterialId,
                DocumentId = m.DocumentId,
                LineId = m.LineId,
                MovementType = m.MovementType.ToString(),
                QuantityDelta = m.QuantityDelta,
                PostedAtUtc = m.PostedAtUtc,
                PostedBy = m.PostedBy
            })
            .OrderBy(m => m.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return movements;
    }
}

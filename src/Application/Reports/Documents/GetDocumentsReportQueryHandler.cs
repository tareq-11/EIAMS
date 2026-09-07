using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reports.Documents;

internal sealed class GetDocumentsReportQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetDocumentsReportQuery, PagedResult<DocumentsReportRow>>
{
    public async Task<Result<PagedResult<DocumentsReportRow>>> Handle(
        GetDocumentsReportQuery query,
        CancellationToken cancellationToken)
    {
        WarehousePermissionScope access = await scopeAuthorizationService.GetWarehousePermissionScopeAsync(
            userContext.UserId, PermissionCodes.WarehouseDocuments.View, cancellationToken);
        if (!access.HasEnterpriseAccess && access.WarehouseIds.Count == 0)
        {
            return Result.Failure<PagedResult<DocumentsReportRow>>(ReportErrors.Forbidden);
        }

        Guid[] warehouseIds = access.WarehouseIds.ToArray();
        DateTime? fromDate = query.FromUtc?.UtcDateTime;
        DateTime? toDate = query.ToUtc?.UtcDateTime;
        IQueryable<DocumentsReportRow> source =
            from document in context.WarehouseDocuments.AsNoTracking()
            join warehouse in context.Warehouses.AsNoTracking() on document.WarehouseId equals warehouse.Id
            where access.HasEnterpriseAccess || warehouseIds.Contains(document.WarehouseId)
            where query.WarehouseId == null || document.WarehouseId == query.WarehouseId
            where query.DocumentType == null || document.DocumentType == query.DocumentType
            where query.Status == null || document.DocumentStatus == query.Status
            where fromDate == null || document.CreatedAtUtc >= fromDate
            where toDate == null || document.CreatedAtUtc < toDate
            group document by new
            {
                warehouse.Id,
                warehouse.Code,
                warehouse.Name,
                document.DocumentType,
                document.DocumentStatus
            }
            into grouped
            orderby grouped.Key.Code, grouped.Key.DocumentType, grouped.Key.DocumentStatus, grouped.Key.Id
            select new DocumentsReportRow(
                grouped.Key.Id,
                grouped.Key.Code,
                grouped.Key.Name,
                grouped.Key.DocumentType.ToString(),
                grouped.Key.DocumentStatus.ToString(),
                grouped.Count(),
                grouped.Max(document => (DateTime?)document.CreatedAtUtc),
                grouped.Max(document => document.PostedAtUtc));

        int totalItems = await source.CountAsync(cancellationToken);
        int offset = checked((query.Page - 1) * query.PageSize);
        List<DocumentsReportRow> items = await source.Skip(offset).Take(query.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<DocumentsReportRow>(items, query.Page, query.PageSize, totalItems);
    }
}

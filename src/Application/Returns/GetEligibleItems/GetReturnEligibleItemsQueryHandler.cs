using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;
using Domain.Custodies;
using Domain.DurableCustodyAllocations;
using Domain.ReturnInfos;
using Domain.TrackedMaterialUnits;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Returns.GetEligibleItems;

internal sealed class GetReturnEligibleItemsQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetReturnEligibleItemsQuery, PagedResult<ReturnEligibleItemResponse>>
{
    public async Task<Result<PagedResult<ReturnEligibleItemResponse>>> Handle(
        GetReturnEligibleItemsQuery query,
        CancellationToken cancellationToken)
    {
        WarehouseDocument? originalIssue = await context.WarehouseDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == query.OriginalIssueDocumentId, cancellationToken);

        if (originalIssue is null ||
            originalIssue.DocumentType != DocumentType.Issue ||
            originalIssue.DocumentStatus != DocumentStatus.Posted)
        {
            return Result.Failure<PagedResult<ReturnEligibleItemResponse>>(
                ReturnInfoErrors.OriginalIssueInvalid(query.OriginalIssueDocumentId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.View,
            ScopeType.Warehouse,
            originalIssue.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<PagedResult<ReturnEligibleItemResponse>>(Error.Forbidden(
                "Returns.Unauthorized",
                "You do not have permission to view items from this issue document."));
        }

        int page = query.Page < PaginationDefaults.DefaultPage
            ? PaginationDefaults.DefaultPage
            : Math.Min(query.Page, PaginationDefaults.MaximumPage);
        int pageSize = query.PageSize < PaginationDefaults.DefaultPage
            ? PaginationDefaults.DefaultPageSize
            : Math.Min(query.PageSize, PaginationDefaults.MaximumPageSize);
        int offset = checked((page - 1) * pageSize);
        int fetchLimit = checked(offset + pageSize);

        int totalCount = 0;
        var candidateRows = new List<EligibleItemPageRow>();

        // 1. Assets issued on this document that are currently active in custody
        var assetQuery = from c in context.Custodies.AsNoTracking()
                         where c.IssueDocumentId == query.OriginalIssueDocumentId && c.Status == CustodyStatus.Active
                         join a in context.Assets.AsNoTracking() on c.AssetId equals a.Id
                         join m in context.Materials.AsNoTracking() on a.MaterialId equals m.Id
                         select new { c, a, m };

        totalCount += await assetQuery.CountAsync(cancellationToken);

        List<EligibleItemPageRow> projectedAssets = await assetQuery
            .OrderByDescending(row => row.c.FromUtc)
            .ThenBy(row => row.a.Id)
            .Take(fetchLimit)
            .Select(row => new EligibleItemPageRow(
                CustodySubjectType.Asset,
                row.a.Id,
                row.m.Id,
                row.m.NameAr,
                row.m.NameEn,
                row.m.Code,
                row.m.MaterialKind.ToString(),
                row.m.TrackingType.ToString(),
                row.a.SerialNumber,
                row.a.AssetNumber,
                1m,
                1m,
                row.c.FromUtc))
            .ToListAsync(cancellationToken);

        candidateRows.AddRange(projectedAssets);

        // 2. Tracked durable units issued on this document that are currently Issued
        var unitQuery = from u in context.TrackedMaterialUnits.AsNoTracking()
                        where u.IssueDocumentId == query.OriginalIssueDocumentId && u.Status == TrackedMaterialUnitStatus.Issued
                        join m in context.Materials.AsNoTracking() on u.MaterialId equals m.Id
                        select new { u, m };

        totalCount += await unitQuery.CountAsync(cancellationToken);

        List<EligibleItemPageRow> projectedUnits = await unitQuery
            .OrderByDescending(row => row.u.FromUtc)
            .ThenBy(row => row.u.Id)
            .Take(fetchLimit)
            .Select(row => new EligibleItemPageRow(
                CustodySubjectType.TrackedUnit,
                row.u.Id,
                row.m.Id,
                row.m.NameAr,
                row.m.NameEn,
                row.m.Code,
                row.m.MaterialKind.ToString(),
                row.m.TrackingType.ToString(),
                row.u.SerialNumber,
                null,
                1m,
                1m,
                row.u.FromUtc))
            .ToListAsync(cancellationToken);

        candidateRows.AddRange(projectedUnits);

        // 3. Durable custody allocations issued on this document with ActiveQuantity > 0
        var allocationQuery = from alloc in context.DurableCustodyAllocations.AsNoTracking()
                              where alloc.IssueDocumentId == query.OriginalIssueDocumentId &&
                                    alloc.Status == DurableCustodyAllocationStatus.Active &&
                                    alloc.ActiveQuantity > 0
                              join m in context.Materials.AsNoTracking() on alloc.MaterialId equals m.Id
                              select new { alloc, m };

        totalCount += await allocationQuery.CountAsync(cancellationToken);

        List<EligibleItemPageRow> projectedAllocations = await allocationQuery
            .OrderByDescending(row => row.alloc.FromUtc)
            .ThenBy(row => row.alloc.Id)
            .Take(fetchLimit)
            .Select(row => new EligibleItemPageRow(
                CustodySubjectType.MaterialQuantity,
                row.alloc.Id,
                row.m.Id,
                row.m.NameAr,
                row.m.NameEn,
                row.m.Code,
                row.m.MaterialKind.ToString(),
                row.m.TrackingType.ToString(),
                null,
                null,
                row.alloc.ActiveQuantity,
                row.alloc.IssuedQuantity,
                row.alloc.FromUtc))
            .ToListAsync(cancellationToken);

        candidateRows.AddRange(projectedAllocations);

        var rows = candidateRows
            .OrderByDescending(row => row.FromUtc)
            .ThenBy(row => row.SubjectType)
            .ThenBy(row => row.SubjectId)
            .Skip(offset)
            .Take(pageSize)
            .ToList();

        var items = rows.Select(row => new ReturnEligibleItemResponse(
            row.SubjectType,
            row.SubjectId,
            row.MaterialId,
            row.MaterialNameAr,
            row.MaterialNameEn,
            row.MaterialCode,
            row.MaterialKind,
            row.TrackingType,
            row.SerialNumber,
            row.AssetNumber,
            row.AvailableQuantity,
            row.IssuedQuantity
        )).ToList();

        return new PagedResult<ReturnEligibleItemResponse>(items, page, pageSize, totalCount);
    }

    private sealed record EligibleItemPageRow(
        CustodySubjectType SubjectType,
        Guid SubjectId,
        Guid MaterialId,
        string MaterialNameAr,
        string? MaterialNameEn,
        string MaterialCode,
        string MaterialKind,
        string TrackingType,
        string? SerialNumber,
        string? AssetNumber,
        decimal AvailableQuantity,
        decimal IssuedQuantity,
        DateTime FromUtc);
}

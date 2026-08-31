using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
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
    : IQueryHandler<GetReturnEligibleItemsQuery, IReadOnlyList<ReturnEligibleItemResponse>>
{
    public async Task<Result<IReadOnlyList<ReturnEligibleItemResponse>>> Handle(
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
            return Result.Failure<IReadOnlyList<ReturnEligibleItemResponse>>(
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
            return Result.Failure<IReadOnlyList<ReturnEligibleItemResponse>>(Error.Forbidden(
                "Returns.Unauthorized",
                "You do not have permission to view items from this issue document."));
        }

        var results = new List<ReturnEligibleItemResponse>();

        // 1. Assets issued on this document that are currently active in custody
        List<ReturnEligibleItemResponse> activeAssets = await (
            from c in context.Custodies.AsNoTracking()
            where c.IssueDocumentId == query.OriginalIssueDocumentId && c.Status == CustodyStatus.Active
            join a in context.Assets.AsNoTracking() on c.AssetId equals a.Id
            join m in context.Materials.AsNoTracking() on a.MaterialId equals m.Id
            select new ReturnEligibleItemResponse(
                CustodySubjectType.Asset,
                a.Id,
                m.Id,
                m.NameAr,
                m.NameEn,
                m.Code,
                m.MaterialKind.ToString(),
                m.TrackingType.ToString(),
                a.SerialNumber,
                a.AssetNumber,
                1m,
                1m)
        ).ToListAsync(cancellationToken);

        results.AddRange(activeAssets);

        // 2. Tracked durable units issued on this document that are currently Issued
        List<ReturnEligibleItemResponse> activeTrackedUnits = await (
            from u in context.TrackedMaterialUnits.AsNoTracking()
            where u.IssueDocumentId == query.OriginalIssueDocumentId && u.Status == TrackedMaterialUnitStatus.Issued
            join m in context.Materials.AsNoTracking() on u.MaterialId equals m.Id
            select new ReturnEligibleItemResponse(
                CustodySubjectType.TrackedUnit,
                u.Id,
                m.Id,
                m.NameAr,
                m.NameEn,
                m.Code,
                m.MaterialKind.ToString(),
                m.TrackingType.ToString(),
                u.SerialNumber,
                null,
                1m,
                1m)
        ).ToListAsync(cancellationToken);

        results.AddRange(activeTrackedUnits);

        // 3. Durable custody allocations issued on this document with ActiveQuantity > 0
        List<ReturnEligibleItemResponse> activeAllocations = await (
            from alloc in context.DurableCustodyAllocations.AsNoTracking()
            where alloc.IssueDocumentId == query.OriginalIssueDocumentId &&
                  alloc.Status == DurableCustodyAllocationStatus.Active &&
                  alloc.ActiveQuantity > 0
            join m in context.Materials.AsNoTracking() on alloc.MaterialId equals m.Id
            select new ReturnEligibleItemResponse(
                CustodySubjectType.MaterialQuantity,
                alloc.Id,
                m.Id,
                m.NameAr,
                m.NameEn,
                m.Code,
                m.MaterialKind.ToString(),
                m.TrackingType.ToString(),
                null,
                null,
                alloc.ActiveQuantity,
                alloc.IssuedQuantity)
        ).ToListAsync(cancellationToken);

        results.AddRange(activeAllocations);

        return results;
    }
}

using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Abstractions.Recipients;
using Domain.Common;
using Domain.Custodies;
using Domain.DurableCustodyAllocations;
using Domain.TrackedMaterialUnits;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Custodies.GetCustodies;

internal sealed class GetCustodiesQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    ICounterpartResolver counterpartResolver)
    : IQueryHandler<GetCustodiesQuery, PagedResult<CustodyResponse>>
{
    public async Task<Result<PagedResult<CustodyResponse>>> Handle(
        GetCustodiesQuery query,
        CancellationToken cancellationToken)
    {
        WarehousePermissionScope access = await scopeAuthorizationService.GetWarehousePermissionScopeAsync(
            userContext.UserId,
            PermissionCodes.Custodies.View,
            cancellationToken);

        if (!access.HasEnterpriseAccess && access.WarehouseIds.Count == 0)
        {
            return Result.Failure<PagedResult<CustodyResponse>>(Error.Forbidden(
                "Custodies.Unauthorized",
                "You do not have permission to view custody records."));
        }

        Guid[] allowedWarehouseIds = access.WarehouseIds.ToArray();

        int page = query.Page <= 0 ? 1 : query.Page;
        int pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        if (pageSize > 100)
        {
            pageSize = 100;
        }

        var allItems = new List<CustodyResponse>();

        // 1. Asset Custodies
        if (query.SubjectType is null or CustodySubjectType.Asset)
        {
            var assetQuery = from c in context.Custodies.AsNoTracking()
                             join a in context.Assets.AsNoTracking() on c.AssetId equals a.Id
                             join m in context.Materials.AsNoTracking() on a.MaterialId equals m.Id
                             join w in context.Warehouses.AsNoTracking() on a.WarehouseId equals w.Id
                             join doc in context.WarehouseDocuments.AsNoTracking() on c.IssueDocumentId equals doc.Id
                             select new
                             {
                                 Custody = c,
                                 Asset = a,
                                 Material = m,
                                 Warehouse = w,
                                 Document = doc
                             } into row
                             where access.HasEnterpriseAccess || allowedWarehouseIds.Contains(row.Warehouse.Id)
                             select row;

            if (query.HolderType.HasValue)
            {
                assetQuery = assetQuery.Where(x => x.Custody.HolderType == query.HolderType.Value);
            }

            if (query.HolderId.HasValue)
            {
                assetQuery = assetQuery.Where(x => x.Custody.HolderId == query.HolderId.Value);
            }

            if (query.MaterialId.HasValue)
            {
                assetQuery = assetQuery.Where(x => x.Asset.MaterialId == query.MaterialId.Value);
            }

            if (query.WarehouseId.HasValue)
            {
                assetQuery = assetQuery.Where(x => x.Asset.WarehouseId == query.WarehouseId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                assetQuery = assetQuery.Where(x => x.Custody.Status.ToString() == query.Status);
            }

            var assetRows = await assetQuery.ToListAsync(cancellationToken);

            foreach (var row in assetRows)
            {
                CounterpartResolution? resolution = await counterpartResolver.ResolveAsync(
                    row.Custody.HolderType,
                    row.Custody.HolderId,
                    cancellationToken);

                allItems.Add(new CustodyResponse(
                    CustodySubjectType.Asset,
                    row.Asset.Id,
                    row.Custody.Id,
                    row.Material.Id,
                    row.Material.NameAr,
                    row.Material.NameEn,
                    row.Material.Code,
                    row.Material.MaterialKind.ToString(),
                    row.Material.TrackingType.ToString(),
                    row.Asset.SerialNumber,
                    row.Asset.AssetNumber,
                    row.Warehouse.Id,
                    row.Warehouse.Name,
                    row.Custody.HolderType,
                    row.Custody.HolderId,
                    resolution?.DisplayName ?? "Unknown",
                    row.Custody.CustodyKind,
                    1m,
                    row.Custody.Status == CustodyStatus.Active ? 1m : 0m,
                    row.Custody.Status == CustodyStatus.Closed ? 1m : 0m,
                    row.Custody.IssueDocumentId,
                    row.Document.SystemReferenceNumber,
                    row.Custody.Status.ToString(),
                    row.Custody.FromUtc,
                    row.Custody.ToUtc,
                    row.Custody.RowVersion));
            }
        }

        // 2. Tracked Unit (Durable + Serial)
        if (query.SubjectType is null or CustodySubjectType.TrackedUnit)
        {
            var unitQuery = from u in context.TrackedMaterialUnits.AsNoTracking()
                            join m in context.Materials.AsNoTracking() on u.MaterialId equals m.Id
                            join w in context.Warehouses.AsNoTracking() on u.WarehouseId equals w.Id
                            join doc in context.WarehouseDocuments.AsNoTracking() on u.IssueDocumentId equals doc.Id
                            select new
                            {
                                Unit = u,
                                Material = m,
                                Warehouse = w,
                                Document = doc
                            } into row
                            where access.HasEnterpriseAccess || allowedWarehouseIds.Contains(row.Warehouse.Id)
                            select row;

            if (query.HolderType.HasValue)
            {
                unitQuery = unitQuery.Where(x => x.Unit.HolderType == query.HolderType.Value);
            }

            if (query.HolderId.HasValue)
            {
                unitQuery = unitQuery.Where(x => x.Unit.HolderId == query.HolderId.Value);
            }

            if (query.MaterialId.HasValue)
            {
                unitQuery = unitQuery.Where(x => x.Unit.MaterialId == query.MaterialId.Value);
            }

            if (query.WarehouseId.HasValue)
            {
                unitQuery = unitQuery.Where(x => x.Unit.WarehouseId == query.WarehouseId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                unitQuery = unitQuery.Where(x => x.Unit.Status.ToString() == query.Status);
            }

            var unitRows = await unitQuery.ToListAsync(cancellationToken);

            foreach (var row in unitRows)
            {
                CounterpartResolution? resolution = await counterpartResolver.ResolveAsync(
                    row.Unit.HolderType,
                    row.Unit.HolderId,
                    cancellationToken);

                allItems.Add(new CustodyResponse(
                    CustodySubjectType.TrackedUnit,
                    row.Unit.Id,
                    row.Unit.Id,
                    row.Material.Id,
                    row.Material.NameAr,
                    row.Material.NameEn,
                    row.Material.Code,
                    row.Material.MaterialKind.ToString(),
                    row.Material.TrackingType.ToString(),
                    row.Unit.SerialNumber,
                    null,
                    row.Warehouse.Id,
                    row.Warehouse.Name,
                    row.Unit.HolderType,
                    row.Unit.HolderId,
                    resolution?.DisplayName ?? "Unknown",
                    row.Unit.CustodyKind,
                    1m,
                    row.Unit.Status == TrackedMaterialUnitStatus.Issued ? 1m : 0m,
                    row.Unit.Status == TrackedMaterialUnitStatus.Returned ? 1m : 0m,
                    row.Unit.IssueDocumentId,
                    row.Document.SystemReferenceNumber,
                    row.Unit.Status.ToString(),
                    row.Unit.FromUtc,
                    row.Unit.ToUtc,
                    row.Unit.RowVersion));
            }
        }

        // 3. Durable Custody Allocation (Durable + Quantity)
        if (query.SubjectType is null or CustodySubjectType.MaterialQuantity)
        {
            var allocQuery = from a in context.DurableCustodyAllocations.AsNoTracking()
                             join m in context.Materials.AsNoTracking() on a.MaterialId equals m.Id
                             join w in context.Warehouses.AsNoTracking() on a.WarehouseId equals w.Id
                             join doc in context.WarehouseDocuments.AsNoTracking() on a.IssueDocumentId equals doc.Id
                             select new
                             {
                                 Allocation = a,
                                 Material = m,
                                 Warehouse = w,
                                 Document = doc
                             } into row
                             where access.HasEnterpriseAccess || allowedWarehouseIds.Contains(row.Warehouse.Id)
                             select row;

            if (query.HolderType.HasValue)
            {
                allocQuery = allocQuery.Where(x => x.Allocation.HolderType == query.HolderType.Value);
            }

            if (query.HolderId.HasValue)
            {
                allocQuery = allocQuery.Where(x => x.Allocation.HolderId == query.HolderId.Value);
            }

            if (query.MaterialId.HasValue)
            {
                allocQuery = allocQuery.Where(x => x.Allocation.MaterialId == query.MaterialId.Value);
            }

            if (query.WarehouseId.HasValue)
            {
                allocQuery = allocQuery.Where(x => x.Allocation.WarehouseId == query.WarehouseId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                allocQuery = allocQuery.Where(x => x.Allocation.Status.ToString() == query.Status);
            }

            var allocRows = await allocQuery.ToListAsync(cancellationToken);

            foreach (var row in allocRows)
            {
                CounterpartResolution? resolution = await counterpartResolver.ResolveAsync(
                    row.Allocation.HolderType,
                    row.Allocation.HolderId,
                    cancellationToken);

                allItems.Add(new CustodyResponse(
                    CustodySubjectType.MaterialQuantity,
                    row.Allocation.Id,
                    row.Allocation.Id,
                    row.Material.Id,
                    row.Material.NameAr,
                    row.Material.NameEn,
                    row.Material.Code,
                    row.Material.MaterialKind.ToString(),
                    row.Material.TrackingType.ToString(),
                    null,
                    null,
                    row.Warehouse.Id,
                    row.Warehouse.Name,
                    row.Allocation.HolderType,
                    row.Allocation.HolderId,
                    resolution?.DisplayName ?? "Unknown",
                    row.Allocation.CustodyKind,
                    row.Allocation.IssuedQuantity,
                    row.Allocation.ActiveQuantity,
                    row.Allocation.ReturnedQuantity,
                    row.Allocation.IssueDocumentId,
                    row.Document.SystemReferenceNumber,
                    row.Allocation.Status.ToString(),
                    row.Allocation.FromUtc,
                    null,
                    row.Allocation.RowVersion));
            }
        }

        int totalCount = allItems.Count;
        var pageItems = allItems
            .OrderByDescending(x => x.FromUtc)
            .ThenBy(x => x.CustodyId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<CustodyResponse>(
            pageItems,
            page,
            pageSize,
            totalCount);
    }
}

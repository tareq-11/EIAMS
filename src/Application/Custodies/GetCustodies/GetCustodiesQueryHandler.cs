using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Abstractions.Recipients;
using Domain.Common;
using Domain.Custodies;
using Domain.DurableCustodyAllocations;
using Domain.Materials;
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
        int pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
        int offset = checked((page - 1) * pageSize);
        int fetchLimit = checked(offset + pageSize);
        string? requestedStatus = string.IsNullOrWhiteSpace(query.Status) ? null : query.Status.Trim();
        var candidateRows = new List<CustodyPageRow>(fetchLimit * 3);
        int totalCount = 0;

        if (query.SubjectType is null or CustodySubjectType.Asset)
        {
            var assetQuery = from custody in context.Custodies.AsNoTracking()
                             join asset in context.Assets.AsNoTracking() on custody.AssetId equals asset.Id
                             join material in context.Materials.AsNoTracking() on asset.MaterialId equals material.Id
                             join warehouse in context.Warehouses.AsNoTracking() on asset.WarehouseId equals warehouse.Id
                             join document in context.WarehouseDocuments.AsNoTracking()
                                 on custody.IssueDocumentId equals document.Id
                             where access.HasEnterpriseAccess || allowedWarehouseIds.Contains(warehouse.Id)
                             select new { custody, asset, material, warehouse, document };

            if (query.HolderType.HasValue)
            {
                assetQuery = assetQuery.Where(row => row.custody.HolderType == query.HolderType.Value);
            }

            if (query.HolderId.HasValue)
            {
                assetQuery = assetQuery.Where(row => row.custody.HolderId == query.HolderId.Value);
            }

            if (query.MaterialId.HasValue)
            {
                assetQuery = assetQuery.Where(row => row.asset.MaterialId == query.MaterialId.Value);
            }

            if (query.WarehouseId.HasValue)
            {
                assetQuery = assetQuery.Where(row => row.asset.WarehouseId == query.WarehouseId.Value);
            }

            if (requestedStatus is not null)
            {
                assetQuery = Enum.TryParse(requestedStatus, true, out CustodyStatus status)
                    ? assetQuery.Where(row => row.custody.Status == status)
                    : assetQuery.Where(_ => false);
            }

            totalCount += await assetQuery.CountAsync(cancellationToken);
            IQueryable<CustodyPageRow> projectedAssets = assetQuery
                .OrderByDescending(row => row.custody.FromUtc)
                .ThenBy(row => row.custody.Id)
                .Take(fetchLimit)
                .Select(row => new CustodyPageRow(
                CustodySubjectType.Asset, row.asset.Id, row.custody.Id,
                row.material.Id, row.material.NameAr, row.material.NameEn, row.material.Code,
                row.material.MaterialKind, row.material.TrackingType,
                row.asset.SerialNumber, row.asset.AssetNumber,
                row.warehouse.Id, row.warehouse.Name,
                row.custody.HolderType, row.custody.HolderId, row.custody.CustodyKind,
                1m,
                row.custody.Status == CustodyStatus.Active ? 1m : 0m,
                row.custody.Status == CustodyStatus.Closed ? 1m : 0m,
                row.custody.IssueDocumentId, row.document.SystemReferenceNumber,
                row.custody.Status, null, null,
                row.custody.FromUtc, row.custody.ToUtc, row.custody.RowVersion));

            candidateRows.AddRange(await projectedAssets.ToListAsync(cancellationToken));
        }

        if (query.SubjectType is null or CustodySubjectType.TrackedUnit)
        {
            var unitQuery = from unit in context.TrackedMaterialUnits.AsNoTracking()
                            join material in context.Materials.AsNoTracking() on unit.MaterialId equals material.Id
                            join warehouse in context.Warehouses.AsNoTracking() on unit.WarehouseId equals warehouse.Id
                            join document in context.WarehouseDocuments.AsNoTracking()
                                on unit.IssueDocumentId equals document.Id
                            where access.HasEnterpriseAccess || allowedWarehouseIds.Contains(warehouse.Id)
                            select new { unit, material, warehouse, document };

            if (query.HolderType.HasValue)
            {
                unitQuery = unitQuery.Where(row => row.unit.HolderType == query.HolderType.Value);
            }

            if (query.HolderId.HasValue)
            {
                unitQuery = unitQuery.Where(row => row.unit.HolderId == query.HolderId.Value);
            }

            if (query.MaterialId.HasValue)
            {
                unitQuery = unitQuery.Where(row => row.unit.MaterialId == query.MaterialId.Value);
            }

            if (query.WarehouseId.HasValue)
            {
                unitQuery = unitQuery.Where(row => row.unit.WarehouseId == query.WarehouseId.Value);
            }

            if (requestedStatus is not null)
            {
                unitQuery = Enum.TryParse(requestedStatus, true, out TrackedMaterialUnitStatus status)
                    ? unitQuery.Where(row => row.unit.Status == status)
                    : unitQuery.Where(_ => false);
            }

            totalCount += await unitQuery.CountAsync(cancellationToken);
            IQueryable<CustodyPageRow> projectedUnits = unitQuery
                .OrderByDescending(row => row.unit.FromUtc)
                .ThenBy(row => row.unit.Id)
                .Take(fetchLimit)
                .Select(row => new CustodyPageRow(
                CustodySubjectType.TrackedUnit, row.unit.Id, row.unit.Id,
                row.material.Id, row.material.NameAr, row.material.NameEn, row.material.Code,
                row.material.MaterialKind, row.material.TrackingType,
                row.unit.SerialNumber, null,
                row.warehouse.Id, row.warehouse.Name,
                row.unit.HolderType, row.unit.HolderId, row.unit.CustodyKind,
                1m,
                row.unit.Status == TrackedMaterialUnitStatus.Issued ? 1m : 0m,
                row.unit.Status == TrackedMaterialUnitStatus.Returned ? 1m : 0m,
                row.unit.IssueDocumentId, row.document.SystemReferenceNumber,
                null, row.unit.Status, null,
                row.unit.FromUtc, row.unit.ToUtc, row.unit.RowVersion));

            candidateRows.AddRange(await projectedUnits.ToListAsync(cancellationToken));
        }

        if (query.SubjectType is null or CustodySubjectType.MaterialQuantity)
        {
            var allocationQuery = from allocation in context.DurableCustodyAllocations.AsNoTracking()
                                  join material in context.Materials.AsNoTracking()
                                      on allocation.MaterialId equals material.Id
                                  join warehouse in context.Warehouses.AsNoTracking()
                                      on allocation.WarehouseId equals warehouse.Id
                                  join document in context.WarehouseDocuments.AsNoTracking()
                                      on allocation.IssueDocumentId equals document.Id
                                  where access.HasEnterpriseAccess || allowedWarehouseIds.Contains(warehouse.Id)
                                  select new { allocation, material, warehouse, document };

            if (query.HolderType.HasValue)
            {
                allocationQuery = allocationQuery.Where(
                    row => row.allocation.HolderType == query.HolderType.Value);
            }

            if (query.HolderId.HasValue)
            {
                allocationQuery = allocationQuery.Where(row => row.allocation.HolderId == query.HolderId.Value);
            }

            if (query.MaterialId.HasValue)
            {
                allocationQuery = allocationQuery.Where(row => row.allocation.MaterialId == query.MaterialId.Value);
            }

            if (query.WarehouseId.HasValue)
            {
                allocationQuery = allocationQuery.Where(
                    row => row.allocation.WarehouseId == query.WarehouseId.Value);
            }

            if (requestedStatus is not null)
            {
                allocationQuery = Enum.TryParse(requestedStatus, true, out DurableCustodyAllocationStatus status)
                    ? allocationQuery.Where(row => row.allocation.Status == status)
                    : allocationQuery.Where(_ => false);
            }

            totalCount += await allocationQuery.CountAsync(cancellationToken);
            IQueryable<CustodyPageRow> projectedAllocations = allocationQuery
                .OrderByDescending(row => row.allocation.FromUtc)
                .ThenBy(row => row.allocation.Id)
                .Take(fetchLimit)
                .Select(row => new CustodyPageRow(
                CustodySubjectType.MaterialQuantity, row.allocation.Id, row.allocation.Id,
                row.material.Id, row.material.NameAr, row.material.NameEn, row.material.Code,
                row.material.MaterialKind, row.material.TrackingType,
                null, null,
                row.warehouse.Id, row.warehouse.Name,
                row.allocation.HolderType, row.allocation.HolderId, row.allocation.CustodyKind,
                row.allocation.IssuedQuantity, row.allocation.ActiveQuantity, row.allocation.ReturnedQuantity,
                row.allocation.IssueDocumentId, row.document.SystemReferenceNumber,
                null, null, row.allocation.Status,
                row.allocation.FromUtc, null, row.allocation.RowVersion));

            candidateRows.AddRange(await projectedAllocations.ToListAsync(cancellationToken));
        }

        var rows = candidateRows
            .OrderByDescending(row => row.FromUtc)
            .ThenBy(row => row.CustodyId)
            .Skip(offset)
            .Take(pageSize)
            .ToList();

        CounterpartReference[] holderReferences = rows
            .Select(row => new CounterpartReference(row.HolderType, row.HolderId))
            .Distinct()
            .ToArray();
        IReadOnlyDictionary<CounterpartReference, CounterpartResolution> holders =
            await counterpartResolver.ResolveManyAsync(holderReferences, cancellationToken);

        var items = rows.Select(row =>
        {
            holders.TryGetValue(
                new CounterpartReference(row.HolderType, row.HolderId),
                out CounterpartResolution? holder);

            return new CustodyResponse(
                row.SubjectType, row.SubjectId, row.CustodyId,
                row.MaterialId, row.MaterialNameAr, row.MaterialNameEn, row.MaterialCode,
                row.MaterialKind.ToString(), row.TrackingType.ToString(),
                row.SerialNumber, row.AssetNumber,
                row.WarehouseId, row.WarehouseName,
                row.HolderType, row.HolderId, holder?.DisplayName ?? "Unknown", row.CustodyKind,
                row.IssuedQuantity, row.ActiveQuantity, row.ReturnedQuantity,
                row.IssueDocumentId, row.SystemReferenceNumber, GetStatus(row),
                row.FromUtc, row.ToUtc, row.RowVersion);
        }).ToList();

        return new PagedResult<CustodyResponse>(items, page, pageSize, totalCount);
    }

    private static string GetStatus(CustodyPageRow row) => row.SubjectType switch
    {
        CustodySubjectType.Asset => row.AssetStatus?.ToString() ?? "Unknown",
        CustodySubjectType.TrackedUnit => row.TrackedUnitStatus?.ToString() ?? "Unknown",
        CustodySubjectType.MaterialQuantity => row.AllocationStatus?.ToString() ?? "Unknown",
        _ => "Unknown"
    };

    private sealed record CustodyPageRow(
        CustodySubjectType SubjectType,
        Guid SubjectId,
        Guid CustodyId,
        Guid MaterialId,
        string MaterialNameAr,
        string? MaterialNameEn,
        string MaterialCode,
        MaterialKind MaterialKind,
        TrackingType TrackingType,
        string? SerialNumber,
        string? AssetNumber,
        Guid WarehouseId,
        string WarehouseName,
        PartyType HolderType,
        Guid HolderId,
        CustodyKind CustodyKind,
        decimal IssuedQuantity,
        decimal ActiveQuantity,
        decimal ReturnedQuantity,
        Guid IssueDocumentId,
        string? SystemReferenceNumber,
        CustodyStatus? AssetStatus,
        TrackedMaterialUnitStatus? TrackedUnitStatus,
        DurableCustodyAllocationStatus? AllocationStatus,
        DateTime FromUtc,
        DateTime? ToUtc,
        int RowVersion);
}

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
        string? requestedStatus = string.IsNullOrWhiteSpace(query.Status) ? null : query.Status.Trim();
        IQueryable<CustodyPageRow>? combinedRows = null;

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

            IQueryable<CustodyPageRow> projectedAssets = assetQuery
                .Select(row => new CustodyPageRow
                {
                    SubjectType = CustodySubjectType.Asset,
                    SubjectId = row.asset.Id,
                    CustodyId = row.custody.Id,
                    MaterialId = row.material.Id,
                    MaterialNameAr = row.material.NameAr,
                    MaterialNameEn = row.material.NameEn,
                    MaterialCode = row.material.Code,
                    MaterialKind = row.material.MaterialKind,
                    TrackingType = row.material.TrackingType,
                    SerialNumber = row.asset.SerialNumber,
                    AssetNumber = row.asset.AssetNumber,
                    WarehouseId = row.warehouse.Id,
                    WarehouseName = row.warehouse.Name,
                    HolderType = row.custody.HolderType,
                    HolderId = row.custody.HolderId,
                    CustodyKind = row.custody.CustodyKind,
                    IssuedQuantity = 1m,
                    ActiveQuantity = row.custody.Status == CustodyStatus.Active ? 1m : 0m,
                    ReturnedQuantity = row.custody.Status == CustodyStatus.Closed ? 1m : 0m,
                    IssueDocumentId = row.custody.IssueDocumentId,
                    SystemReferenceNumber = row.document.SystemReferenceNumber,
                    StatusIsFirst = row.custody.Status == CustodyStatus.Active,
                    StatusIsSecond = row.custody.Status == CustodyStatus.Closed,
                    FromUtc = row.custody.FromUtc,
                    ToUtc = row.custody.ToUtc,
                    RowVersion = row.custody.RowVersion
                });

            combinedRows = Append(combinedRows, projectedAssets);
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

            IQueryable<CustodyPageRow> projectedUnits = unitQuery
                .Select(row => new CustodyPageRow
                {
                    SubjectType = CustodySubjectType.TrackedUnit,
                    SubjectId = row.unit.Id,
                    CustodyId = row.unit.Id,
                    MaterialId = row.material.Id,
                    MaterialNameAr = row.material.NameAr,
                    MaterialNameEn = row.material.NameEn,
                    MaterialCode = row.material.Code,
                    MaterialKind = row.material.MaterialKind,
                    TrackingType = row.material.TrackingType,
                    SerialNumber = row.unit.SerialNumber,
                    AssetNumber = null,
                    WarehouseId = row.warehouse.Id,
                    WarehouseName = row.warehouse.Name,
                    HolderType = row.unit.HolderType,
                    HolderId = row.unit.HolderId,
                    CustodyKind = row.unit.CustodyKind,
                    IssuedQuantity = 1m,
                    ActiveQuantity = row.unit.Status == TrackedMaterialUnitStatus.Issued ? 1m : 0m,
                    ReturnedQuantity = row.unit.Status == TrackedMaterialUnitStatus.Returned ? 1m : 0m,
                    IssueDocumentId = row.unit.IssueDocumentId,
                    SystemReferenceNumber = row.document.SystemReferenceNumber,
                    StatusIsFirst = row.unit.Status == TrackedMaterialUnitStatus.Issued,
                    StatusIsSecond = row.unit.Status == TrackedMaterialUnitStatus.Returned,
                    FromUtc = row.unit.FromUtc,
                    ToUtc = row.unit.ToUtc,
                    RowVersion = row.unit.RowVersion
                });

            combinedRows = Append(combinedRows, projectedUnits);
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

            IQueryable<CustodyPageRow> projectedAllocations = allocationQuery
                .Select(row => new CustodyPageRow
                {
                    SubjectType = CustodySubjectType.MaterialQuantity,
                    SubjectId = row.allocation.Id,
                    CustodyId = row.allocation.Id,
                    MaterialId = row.material.Id,
                    MaterialNameAr = row.material.NameAr,
                    MaterialNameEn = row.material.NameEn,
                    MaterialCode = row.material.Code,
                    MaterialKind = row.material.MaterialKind,
                    TrackingType = row.material.TrackingType,
                    SerialNumber = null,
                    AssetNumber = null,
                    WarehouseId = row.warehouse.Id,
                    WarehouseName = row.warehouse.Name,
                    HolderType = row.allocation.HolderType,
                    HolderId = row.allocation.HolderId,
                    CustodyKind = row.allocation.CustodyKind,
                    IssuedQuantity = row.allocation.IssuedQuantity,
                    ActiveQuantity = row.allocation.ActiveQuantity,
                    ReturnedQuantity = row.allocation.ReturnedQuantity,
                    IssueDocumentId = row.allocation.IssueDocumentId,
                    SystemReferenceNumber = row.document.SystemReferenceNumber,
                    StatusIsFirst = row.allocation.Status == DurableCustodyAllocationStatus.Active,
                    StatusIsSecond = row.allocation.Status == DurableCustodyAllocationStatus.FullyReturned,
                    FromUtc = row.allocation.FromUtc,
                    ToUtc = null,
                    RowVersion = row.allocation.RowVersion
                });

            combinedRows = Append(combinedRows, projectedAllocations);
        }

        if (combinedRows is null)
        {
            return new PagedResult<CustodyResponse>([], page, pageSize, 0);
        }

        int totalCount = await combinedRows.CountAsync(cancellationToken);
        List<CustodyPageRow> rows = await combinedRows
            .OrderByDescending(row => row.FromUtc)
            .ThenBy(row => row.SubjectType)
            .ThenBy(row => row.CustodyId)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

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

    private static IQueryable<CustodyPageRow> Append(
        IQueryable<CustodyPageRow>? current,
        IQueryable<CustodyPageRow> next) =>
        current is null ? next : current.Concat(next);

    private static string GetStatus(CustodyPageRow row)
    {
        if (row.SubjectType == CustodySubjectType.Asset)
        {
            return row.StatusIsFirst ? "Active" : "Closed";
        }

        if (row.SubjectType == CustodySubjectType.TrackedUnit)
        {
            if (row.StatusIsFirst)
            {
                return "Issued";
            }

            return row.StatusIsSecond ? "Returned" : "Disposed";
        }

        if (row.SubjectType == CustodySubjectType.MaterialQuantity)
        {
            return row.StatusIsFirst ? "Active" : "FullyReturned";
        }

        return "Unknown";
    }

    private sealed class CustodyPageRow
    {
        public CustodySubjectType SubjectType { get; init; }
        public Guid SubjectId { get; init; }
        public Guid CustodyId { get; init; }
        public Guid MaterialId { get; init; }
        public required string MaterialNameAr { get; init; }
        public string? MaterialNameEn { get; init; }
        public required string MaterialCode { get; init; }
        public MaterialKind MaterialKind { get; init; }
        public TrackingType TrackingType { get; init; }
        public string? SerialNumber { get; init; }
        public string? AssetNumber { get; init; }
        public Guid WarehouseId { get; init; }
        public required string WarehouseName { get; init; }
        public PartyType HolderType { get; init; }
        public Guid HolderId { get; init; }
        public CustodyKind CustodyKind { get; init; }
        public decimal IssuedQuantity { get; init; }
        public decimal ActiveQuantity { get; init; }
        public decimal ReturnedQuantity { get; init; }
        public Guid IssueDocumentId { get; init; }
        public string? SystemReferenceNumber { get; init; }
        public bool StatusIsFirst { get; init; }
        public bool StatusIsSecond { get; init; }
        public DateTime FromUtc { get; init; }
        public DateTime? ToUtc { get; init; }
        public int RowVersion { get; init; }
    }
}

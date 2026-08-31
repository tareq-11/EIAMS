using Application.Abstractions.Data;
using Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Application.StockMovements.GetList;

internal static class StockMovementQuerySupport
{
    public static IQueryable<StockMovementResponse> Build(
        IApplicationDbContext context,
        bool hasEnterpriseAccess,
        Guid[] allowedWarehouseIds,
        Guid? warehouseId = null,
        Guid? materialId = null,
        Guid? documentId = null,
        MovementType? movementType = null,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        string? search = null,
        Guid? movementId = null)
    {
        string? normalizedSearch = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim().ToUpperInvariant();
        DateTime? fromDate = fromUtc?.UtcDateTime;
        DateTime? toDate = toUtc?.UtcDateTime;

        return
            from movement in context.StockMovements.AsNoTracking()
            join warehouse in context.Warehouses.AsNoTracking() on movement.WarehouseId equals warehouse.Id
            join material in context.Materials.AsNoTracking() on movement.MaterialId equals material.Id
            join document in context.WarehouseDocuments.AsNoTracking() on movement.DocumentId equals document.Id
            where hasEnterpriseAccess || allowedWarehouseIds.Contains(movement.WarehouseId)
            where movementId == null || movement.Id == movementId
            where warehouseId == null || movement.WarehouseId == warehouseId
            where materialId == null || movement.MaterialId == materialId
            where documentId == null || movement.DocumentId == documentId
            where movementType == null || movement.MovementType == movementType
            where fromDate == null || movement.PostedAtUtc >= fromDate
            where toDate == null || movement.PostedAtUtc < toDate
            where normalizedSearch == null ||
#pragma warning disable CA1304, CA1311 // Translated by EF Core to the database UPPER function.
                  EF.Functions.Like(warehouse.Code.ToUpper(), $"%{normalizedSearch}%") ||
                  EF.Functions.Like(warehouse.Name.ToUpper(), $"%{normalizedSearch}%") ||
                  EF.Functions.Like(material.Code.ToUpper(), $"%{normalizedSearch}%") ||
                  EF.Functions.Like(material.NameAr.ToUpper(), $"%{normalizedSearch}%") ||
                  EF.Functions.Like(document.SystemReferenceNumber.ToUpper(), $"%{normalizedSearch}%")
#pragma warning restore CA1304, CA1311
            orderby movement.PostedAtUtc descending, movement.Id descending
            select new StockMovementResponse(
                movement.Id,
                movement.WarehouseId,
                warehouse.Code,
                warehouse.Name,
                movement.MaterialId,
                material.Code,
                material.NameAr,
                movement.DocumentId,
                document.SystemReferenceNumber,
                movement.LineId,
                movement.MovementType.ToString(),
                movement.QuantityDelta,
                movement.PostedAtUtc,
                movement.PostedBy);
    }
}

using SharedKernel;

namespace Domain.Warehouses;

public static class WarehouseErrors
{
    public static Error NotFound(Guid warehouseId) => Error.NotFound(
        "Warehouses.NotFound",
        $"The warehouse with the Id = '{warehouseId}' was not found",
        new { warehouse_id = warehouseId });

    public static Error CodeNotUnique(string code) => Error.Conflict(
        "Warehouses.CodeNotUnique",
        "The provided warehouse code is not unique",
        new { code });

    public static Error SiteNotFound(Guid siteId) => Error.NotFound(
        "Warehouses.SiteNotFound",
        $"The site with the Id = '{siteId}' was not found",
        new { site_id = siteId });

    public static Error SiteInactive(Guid siteId) => Error.Problem(
        "Warehouses.SiteInactive",
        $"The site with the Id = '{siteId}' is inactive and cannot receive a new warehouse.",
        new { site_id = siteId });

    public static Error OrganizationalUnitNotFound(Guid organizationalUnitId) => Error.NotFound(
        "Warehouses.OrganizationalUnitNotFound",
        $"The organizational unit with the Id = '{organizationalUnitId}' was not found",
        new { organizational_unit_id = organizationalUnitId });

    public static Error OrganizationalUnitInactive(Guid organizationalUnitId) => Error.Problem(
        "Warehouses.OrganizationalUnitInactive",
        $"The organizational unit with the Id = '{organizationalUnitId}' is inactive.",
        new { organizational_unit_id = organizationalUnitId });

    public static Error OrganizationalUnitInDifferentSite(Guid organizationalUnitId, Guid siteId) => Error.Conflict(
        "Warehouses.OrganizationalUnitInDifferentSite",
        "The warehouse administrative owner must belong to the same site as the warehouse.",
        new { organizational_unit_id = organizationalUnitId, site_id = siteId });

    public static Error Inactive(Guid warehouseId) => Error.Problem(
        "Warehouses.Inactive",
        $"The warehouse with the Id = '{warehouseId}' is inactive.",
        new { warehouse_id = warehouseId });

    public static Error CannotHoldStock(Guid warehouseId) => Error.Problem(
        "Warehouses.CannotHoldStock",
        $"The warehouse with the Id = '{warehouseId}' cannot hold stock.",
        new { warehouse_id = warehouseId });

    public static Error RowVersionMismatch(Guid warehouseId, int expectedRowVersion, int? currentRowVersion) => Error.Conflict(
        "Warehouses.RowVersionMismatch",
        "The warehouse was modified by another request; reload and try again.",
        new
        {
            warehouse_id = warehouseId,
            expected_row_version = expectedRowVersion,
            current_row_version = currentRowVersion
        });

    public static readonly Error Forbidden = Error.Forbidden(
        "Warehouses.Forbidden",
        "You are not authorized to manage warehouses.");
}

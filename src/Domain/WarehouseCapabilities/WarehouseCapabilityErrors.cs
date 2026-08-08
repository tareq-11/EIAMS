using SharedKernel;

namespace Domain.WarehouseCapabilities;

public static class WarehouseCapabilityErrors
{
    public static Error NotFound(Guid capabilityId) => Error.NotFound(
        "WarehouseCapabilities.NotFound",
        $"The warehouse capability with the Id = '{capabilityId}' was not found",
        new { capability_id = capabilityId });

    public static Error AlreadyGranted(Guid warehouseId, Guid materialDomainId) => Error.Conflict(
        "WarehouseCapabilities.AlreadyGranted",
        "The warehouse already has this material domain granted and active.",
        new { warehouse_id = warehouseId, material_domain_id = materialDomainId });

    public static Error AlreadyRevoked(Guid capabilityId) => Error.Conflict(
        "WarehouseCapabilities.AlreadyRevoked",
        "The warehouse capability is already inactive.",
        new { capability_id = capabilityId });

    public static Error MaterialDomainNotFound(Guid materialDomainId) => Error.NotFound(
        "WarehouseCapabilities.MaterialDomainNotFound",
        $"The material domain with the Id = '{materialDomainId}' was not found",
        new { material_domain_id = materialDomainId });

    public static Error MaterialDomainInactive(Guid materialDomainId) => Error.Problem(
        "WarehouseCapabilities.MaterialDomainInactive",
        "The material domain is inactive and cannot be granted to a warehouse.",
        new { material_domain_id = materialDomainId });

    public static Error NotGranted(Guid warehouseId, Guid materialDomainId) => Error.Problem(
        "WarehouseCapabilities.NotGranted",
        $"The warehouse with the Id = '{warehouseId}' does not have an active capability for the " +
        $"material domain with the Id = '{materialDomainId}'.",
        new { warehouse_id = warehouseId, material_domain_id = materialDomainId });

    public static readonly Error Forbidden = Error.Forbidden(
        "WarehouseCapabilities.Forbidden",
        "You are not authorized to manage warehouse capabilities.");
}

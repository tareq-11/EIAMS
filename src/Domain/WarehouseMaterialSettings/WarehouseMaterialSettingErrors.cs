using SharedKernel;

namespace Domain.WarehouseMaterialSettings;

public static class WarehouseMaterialSettingErrors
{
    public static Error NotFound(Guid settingId) => Error.NotFound(
        "WarehouseMaterialSettings.NotFound",
        $"The warehouse material setting with the Id = '{settingId}' was not found",
        new { setting_id = settingId });

    public static Error AlreadyExists(Guid warehouseId, Guid materialId) => Error.Conflict(
        "WarehouseMaterialSettings.AlreadyExists",
        "A setting for this material already exists in this warehouse.",
        new { warehouse_id = warehouseId, material_id = materialId });

    public static Error InvalidRange(decimal minQuantity, decimal maxQuantity) => Error.Problem(
        "WarehouseMaterialSettings.InvalidRange",
        "MinQuantity and MaxQuantity must be non-negative, and MinQuantity must not exceed MaxQuantity.",
        new { min_quantity = minQuantity, max_quantity = maxQuantity });

    public static Error WarehouseCannotHoldStock(Guid warehouseId) => Error.Problem(
        "WarehouseMaterialSettings.WarehouseCannotHoldStock",
        "The warehouse cannot hold stock, so material settings cannot be created or activated.",
        new { warehouse_id = warehouseId });

    public static Error MaterialNotFound(Guid materialId) => Error.NotFound(
        "WarehouseMaterialSettings.MaterialNotFound",
        $"The material with the Id = '{materialId}' was not found",
        new { material_id = materialId });

    public static Error MaterialNotActive(Guid materialId) => Error.Problem(
        "WarehouseMaterialSettings.MaterialNotActive",
        "The material must be active to configure a warehouse setting for it.",
        new { material_id = materialId });

    public static readonly Error Forbidden = Error.Forbidden(
        "WarehouseMaterialSettings.Forbidden",
        "You are not authorized to manage warehouse material settings.");
}

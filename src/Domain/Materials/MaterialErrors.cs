using SharedKernel;

namespace Domain.Materials;

public static class MaterialErrors
{
    public static Error NotFound(Guid materialId) => Error.NotFound(
        "Materials.NotFound",
        $"The material with the Id = '{materialId}' was not found");

    public static readonly Error CodeNotUnique = Error.Conflict(
        "Materials.CodeNotUnique",
        "The provided material code is not unique");

    public static Error FamilyNotFound(Guid familyId) => Error.NotFound(
        "Materials.FamilyNotFound",
        $"The material family with the Id = '{familyId}' was not found");

    public static Error UnitNotFound(Guid unitId) => Error.NotFound(
        "Materials.UnitNotFound",
        $"The unit of measure with the Id = '{unitId}' was not found");

    public static Error UnitNotActive(Guid unitId) => Error.Problem(
        "Materials.UnitNotActive",
        $"The unit of measure with the Id = '{unitId}' is inactive.");

    public static readonly Error FamilyNotActive = Error.Problem(
        "Materials.FamilyNotActive",
        "The material family must be active for a material to be classified under it");

    public static readonly Error CategoryNotActive = Error.Problem(
        "Materials.CategoryNotActive",
        "The material category of the material family must be active");

    public static readonly Error DomainNotActive = Error.Problem(
        "Materials.DomainNotActive",
        "The material domain of the material family's category must be active");

    public static readonly Error ConsumableMustBeQuantityTracked = Error.Problem(
        "Materials.ConsumableMustBeQuantityTracked",
        "Consumable materials must use Quantity tracking type");

    public static readonly Error AssetMustBeSerialTracked = Error.Problem(
        "Materials.AssetMustBeSerialTracked",
        "Asset materials must use Serial tracking type");

    public static Error ClassificationLocked(Guid materialId) => Error.Conflict(
        "Materials.ClassificationLocked",
        $"The material with the Id = '{materialId}' is already used by an operational document, so its kind and tracking type can no longer be changed.",
        new { material_id = materialId });

    public static readonly Error Forbidden = Error.Forbidden(
        "Materials.Forbidden",
        "You are not authorized to manage materials.");

    public static readonly Error ArchivedIsTerminal = Error.Conflict(
        "Materials.ArchivedIsTerminal",
        "An archived material cannot be activated or deactivated.");

    public static Error CatalogVersionMismatch(Guid materialId, int expectedVersion, int? currentVersion) =>
        Error.Conflict(
            "Materials.CatalogVersionMismatch",
            currentVersion is null
                ? $"The material with Id = '{materialId}' was modified or removed by another request. Expected catalog version {expectedVersion}. Refresh and retry."
                : $"The material with Id = '{materialId}' was modified by another request. Expected catalog version {expectedVersion} but found {currentVersion.Value}. Refresh and retry.",
            new { material_id = materialId, expected_catalog_version = expectedVersion, current_catalog_version = currentVersion });
}

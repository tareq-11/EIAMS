using SharedKernel;

namespace Domain.TrackedMaterialUnits;

public static class TrackedMaterialUnitErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("TRACKED_MATERIAL_UNIT_NOT_FOUND", $"Tracked material unit '{id}' was not found.");

    public static Error IdentityRequired =>
        Error.Problem("TRACKED_MATERIAL_UNIT_IDENTITY_REQUIRED", "Unit identity, material, and holder are required.");

    public static Error SerialNumberRequired =>
        Error.Problem("TRACKED_MATERIAL_UNIT_SERIAL_REQUIRED", "Serial number is required.");

    public static Error NotActive =>
        Error.Conflict("TRACKED_MATERIAL_UNIT_NOT_ACTIVE", "Tracked material unit is not currently issued/active.");

    public static Error ReturnDocumentRequired =>
        Error.Problem("TRACKED_MATERIAL_UNIT_RETURN_DOCUMENT_REQUIRED", "Return document ID is required to return a unit.");

    public static Error CloseTimeInvalid =>
        Error.Problem("TRACKED_MATERIAL_UNIT_CLOSE_TIME_INVALID", "Return timestamp must be after issue timestamp.");

    public static Error HolderTypeInvalid =>
        Error.Problem("TRACKED_MATERIAL_UNIT_HOLDER_TYPE_INVALID", "Holder type is invalid.");

    public static Error CustodyKindInvalid =>
        Error.Problem("TRACKED_MATERIAL_UNIT_CUSTODY_KIND_INVALID", "Custody kind is invalid.");

    public static Error PersonalRequiresEmployee =>
        Error.Problem("TRACKED_MATERIAL_UNIT_PERSONAL_REQUIRES_EMPLOYEE", "Personal custody requires an Employee holder.");

    public static Error OperationalRequiresNonEmployee =>
        Error.Problem("TRACKED_MATERIAL_UNIT_OPERATIONAL_REQUIRES_NON_EMPLOYEE", "Operational custody cannot be assigned directly to an individual Employee.");
}

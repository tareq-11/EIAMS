using SharedKernel;

namespace Domain.DurableCustodyAllocations;

public static class DurableCustodyAllocationErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("DURABLE_CUSTODY_ALLOCATION_NOT_FOUND", $"Durable custody allocation '{id}' was not found.");

    public static Error IdentityRequired =>
        Error.Problem("DURABLE_CUSTODY_ALLOCATION_IDENTITY_REQUIRED", "Allocation identity, material, and holder are required.");

    public static Error QuantityMustBePositive =>
        Error.Problem("DURABLE_CUSTODY_ALLOCATION_QUANTITY_MUST_BE_POSITIVE", "Quantity must be positive.");

    public static Error InvalidQuantity => QuantityMustBePositive;

    public static Error ReturnQuantityExceedsActive(decimal requested, decimal active) =>
        Error.Problem(
            "DURABLE_CUSTODY_ALLOCATION_RETURN_EXCEEDS_ACTIVE",
            $"Return quantity '{requested}' exceeds active allocated quantity '{active}'.");

    public static Error NotActive =>
        Error.Conflict("DURABLE_CUSTODY_ALLOCATION_NOT_ACTIVE", "Durable custody allocation is already fully returned or inactive.");

    public static Error ReturnDocumentRequired =>
        Error.Problem("DURABLE_CUSTODY_ALLOCATION_RETURN_DOCUMENT_REQUIRED", "Return document ID is required.");

    public static Error HolderTypeInvalid =>
        Error.Problem("DURABLE_CUSTODY_ALLOCATION_HOLDER_TYPE_INVALID", "Holder type is invalid.");

    public static Error CustodyKindInvalid =>
        Error.Problem("DURABLE_CUSTODY_ALLOCATION_CUSTODY_KIND_INVALID", "Custody kind is invalid.");

    public static Error PersonalRequiresEmployee =>
        Error.Problem("DURABLE_CUSTODY_ALLOCATION_PERSONAL_REQUIRES_EMPLOYEE", "Personal custody requires an Employee holder.");

    public static Error OperationalRequiresNonEmployee =>
        Error.Problem("DURABLE_CUSTODY_ALLOCATION_OPERATIONAL_REQUIRES_NON_EMPLOYEE", "Operational custody cannot be assigned directly to an individual Employee.");
}

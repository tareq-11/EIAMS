using SharedKernel;

namespace Domain.InventoryAdjustments;

public static class InventoryAdjustmentErrors
{
    public static Error NotFound(Guid documentId) => Error.NotFound("InventoryAdjustments.NotFound", "Inventory adjustment was not found.", new { document_id = documentId });
    public static Error Required(Guid documentId) => Error.Problem("InventoryAdjustments.Required", "Adjustment details are required.", new { document_id = documentId });
    public static Error WrongDocumentType(Guid documentId) => Error.Problem("InventoryAdjustments.WrongDocumentType", "Adjustment details can only be attached to an Adjustment document.", new { document_id = documentId });
    public static readonly Error IdentityRequired = Error.Problem("InventoryAdjustments.IdentityRequired", "Adjustment identity is required.");
    public static readonly Error KindInvalid = Error.Problem("InventoryAdjustments.KindInvalid", "Adjustment kind is invalid.");
    public static readonly Error ReasonRequired = Error.Problem("InventoryAdjustments.ReasonRequired", "Adjustment reason is required.");
    public static readonly Error ReasonTooLong = Error.Problem("InventoryAdjustments.ReasonTooLong", "Adjustment reason cannot exceed 500 characters.");
    public static Error InvalidTransition(Guid adjustmentId) => Error.Problem("InventoryAdjustments.InvalidTransition", "The adjustment cannot move to the requested state.", new { adjustment_id = adjustmentId });
    public static Error AlreadyExistsForCount(Guid countId) => Error.Conflict("InventoryAdjustments.AlreadyExistsForCount", "An adjustment already exists for this count.", new { count_id = countId });
    public static Error DisposalReversalNotAllowed(Guid adjustmentId) => Error.Conflict("Disposals.ReversalNotAllowed", "A disposal is terminal and cannot be reversed.", new { adjustment_id = adjustmentId });
    public static Error CountSourceDrifted(Guid countId) => Error.Conflict(
        "InventoryAdjustments.CountSourceDrifted",
        "The adjustment no longer exactly matches its closed inventory count.",
        new { count_id = countId });
    public static Error CountLinkedImmutable(Guid adjustmentId) => Error.Conflict(
        "InventoryAdjustments.CountLinkedImmutable",
        "Lines generated from an inventory count cannot be edited manually.",
        new { adjustment_id = adjustmentId });
}

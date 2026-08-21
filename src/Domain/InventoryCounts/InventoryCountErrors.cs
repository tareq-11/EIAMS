using SharedKernel;

namespace Domain.InventoryCounts;

public static class InventoryCountErrors
{
    public static Error NotFound(Guid countId) => Error.NotFound("InventoryCounts.NotFound", "Inventory count was not found.", new { count_id = countId });
    public static readonly Error IdentityRequired = Error.Problem("InventoryCounts.IdentityRequired", "Inventory count identity values are required.");
    public static readonly Error InvalidType = Error.Problem("InventoryCounts.InvalidType", "Inventory count type is invalid.");
    public static readonly Error InvalidScope = Error.Problem("InventoryCounts.InvalidScope", "Inventory count scope is invalid.");
    public static readonly Error InvalidFreezePolicy = Error.Problem("InventoryCounts.InvalidFreezePolicy", "Inventory freeze policy is invalid.");
    public static readonly Error ScopeReferenceInvalid = Error.Problem("InventoryCounts.ScopeReferenceInvalid", "The scope reference does not match the selected scope type.");
    public static Error InvalidTransition(Guid countId, Domain.Common.InventoryCountStatus current, Domain.Common.InventoryCountStatus target) => Error.Problem("InventoryCounts.InvalidTransition", "The inventory count cannot move to the requested state.", new { count_id = countId, current_status = current.ToString(), target_status = target.ToString() });
    public static Error RowVersionMismatch(Guid countId, int expected, int? current) => Error.Conflict("InventoryCounts.RowVersionMismatch", "The inventory count was modified by another request.", new { count_id = countId, expected_row_version = expected, current_row_version = current });
    public static Error AnotherCountInProgress(Guid warehouseId) => Error.Conflict("InventoryCounts.AnotherCountInProgress", "Another inventory count is already in progress for this warehouse.", new { warehouse_id = warehouseId });
    public static Error SnapshotEmpty(Guid countId) => Error.Problem("InventoryCounts.SnapshotEmpty", "The inventory count snapshot has no lines.", new { count_id = countId });
    public static Error ActualsIncomplete(Guid countId) => Error.Problem("InventoryCounts.ActualsIncomplete", "Every count line must have an actual quantity before completion.", new { count_id = countId });
    public static Error VarianceReasonsRequired(Guid countId) => Error.Problem("InventoryCounts.VarianceReasonsRequired", "Every non-zero variance requires a reason before closing.", new { count_id = countId });
    public static Error PostingBlocked(Guid countId, Guid warehouseId) => Error.Conflict("InventoryCounts.PostingBlocked", "Posting is blocked by an active hard-freeze inventory count.", new { count_id = countId, warehouse_id = warehouseId });
    public static readonly Error TimestampInvalid = Error.Problem("InventoryCounts.TimestampInvalid", "Inventory count transition timestamps must be chronological.");
}

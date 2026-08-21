using SharedKernel;

namespace Domain.InventoryCounts;

public static class InventoryCountLineErrors
{
    public static Error NotFound(Guid lineId) => Error.NotFound("InventoryCountLines.NotFound", "Inventory count line was not found.", new { line_id = lineId });
    public static readonly Error IdentityRequired = Error.Problem("InventoryCountLines.IdentityRequired", "Inventory count line identity values are required.");
    public static readonly Error QuantityInvalid = Error.Problem("InventoryCountLines.QuantityInvalid", "Count quantities must be non-negative decimal(18,3) values.");
    public static readonly Error AssetQuantityInvalid = Error.Problem("InventoryCountLines.AssetQuantityInvalid", "An asset count quantity must be zero or one.");
    public static readonly Error ActualRequired = Error.Problem("InventoryCountLines.ActualRequired", "Actual quantity has not been entered.");
    public static readonly Error VarianceReasonRequired = Error.Problem("InventoryCountLines.VarianceReasonRequired", "A non-zero variance requires a reason.");
    public static readonly Error VarianceReasonTooLong = Error.Problem("InventoryCountLines.VarianceReasonTooLong", "Variance reason cannot exceed 200 characters.");
}

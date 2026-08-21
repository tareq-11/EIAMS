using SharedKernel;

namespace Domain.AssetMovementHistories;

public static class AssetMovementHistoryErrors
{
    public static readonly Error IdentityRequired = Error.Problem("AssetMovementHistories.IdentityRequired", "Asset movement history identity values are required.");
    public static readonly Error MovementTypeInvalid = Error.Problem("AssetMovementHistories.MovementTypeInvalid", "AssetMovementType must be a known value.");
}

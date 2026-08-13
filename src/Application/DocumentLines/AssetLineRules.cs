using Domain.Common;
using Domain.DocumentLines;
using SharedKernel;

namespace Application.DocumentLines;

internal static class AssetLineRules
{
    public static Result Validate(
        Guid lineId,
        DocumentLineType lineType,
        decimal baseQuantity,
        int maxAssetsPerLine)
    {
        if (lineType != DocumentLineType.Asset)
        {
            return Result.Success();
        }

        if (decimal.Truncate(baseQuantity) != baseQuantity)
        {
            return Result.Failure(DocumentLineErrors.AssetQuantityMustBeWhole(lineId, baseQuantity));
        }

        if (baseQuantity > maxAssetsPerLine)
        {
            return Result.Failure(DocumentLineErrors.AssetQuantityLimitExceeded(
                lineId,
                baseQuantity,
                maxAssetsPerLine));
        }

        return Result.Success();
    }
}

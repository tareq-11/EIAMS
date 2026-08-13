using Application.Abstractions.Assets;
using Domain.Assets;
using Domain.Common;
using Domain.DocumentLines;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Infrastructure.Assets;

internal sealed class ReceivedAssetFactory(
    IAssetNumberGenerator assetNumberGenerator,
    IOptions<AssetCreationOptions> options) : IReceivedAssetFactory
{
    public Result<IReadOnlyList<Asset>> CreateForLine(
        DocumentLine line,
        Guid warehouseId,
        DateTime postedAtUtc)
    {
        if (line.LineType != DocumentLineType.Asset)
        {
            return Array.Empty<Asset>();
        }

        if (decimal.Truncate(line.BaseQuantity) != line.BaseQuantity)
        {
            return Result.Failure<IReadOnlyList<Asset>>(
                DocumentLineErrors.AssetQuantityMustBeWhole(line.Id, line.BaseQuantity));
        }

        if (line.BaseQuantity > options.Value.MaxAssetsPerLine)
        {
            return Result.Failure<IReadOnlyList<Asset>>(
                DocumentLineErrors.AssetQuantityLimitExceeded(
                    line.Id,
                    line.BaseQuantity,
                    options.Value.MaxAssetsPerLine));
        }

        int count = decimal.ToInt32(line.BaseQuantity);
        var assets = new List<Asset>(count);
        var acquisitionDate = DateOnly.FromDateTime(postedAtUtc);

        for (int index = 0; index < count; index++)
        {
            var assetId = Guid.NewGuid();
            Result<Asset> assetResult = Asset.CreateReceived(
                assetId,
                line.MaterialId,
                warehouseId,
                line.Id,
                assetNumberGenerator.Generate(assetId),
                acquisitionDate);

            if (assetResult.IsFailure)
            {
                return Result.Failure<IReadOnlyList<Asset>>(assetResult.Error);
            }

            assets.Add(assetResult.Value);
        }

        return assets;
    }
}

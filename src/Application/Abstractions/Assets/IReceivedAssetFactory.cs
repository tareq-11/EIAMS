using Domain.Assets;
using Domain.DocumentLines;
using SharedKernel;

namespace Application.Abstractions.Assets;

/// <summary>Creates, but does not persist, one Asset for each base unit of an asset line.</summary>
public interface IReceivedAssetFactory
{
    Result<IReadOnlyList<Asset>> CreateForLine(
        DocumentLine line,
        Guid warehouseId,
        DateTime postedAtUtc);
}

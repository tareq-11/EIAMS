using Domain.Assets;
using Domain.WarehouseDocuments;

namespace Application.Abstractions.Assets;

public interface IAssetUsageChecker
{
    Task<bool> HasDownstreamUsageAsync(
        IReadOnlyCollection<Asset> assets,
        WarehouseDocument source,
        Guid reversalDocumentId,
        CancellationToken cancellationToken);
}

using Application.Abstractions.Assets;
using Application.Abstractions.Data;
using Domain.Assets;
using Domain.Common;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Assets;

internal sealed class AssetUsageChecker(IApplicationDbContext context) : IAssetUsageChecker
{
    public async Task<bool> HasDownstreamUsageAsync(
        IReadOnlyCollection<Asset> assets,
        WarehouseDocument source,
        Guid reversalDocumentId,
        CancellationToken cancellationToken)
    {
        if (assets.Count == 0)
        {
            return false;
        }

        // A posted source must always have a posting time. Treat corrupt/incomplete state
        // conservatively so that reversal never deletes assets without a reliable boundary.
        if (source.PostedAtUtc is null)
        {
            return true;
        }

        Guid[] materialIds = assets
            .Select(asset => asset.MaterialId)
            .Distinct()
            .ToArray();
        MovementType[] outboundMovementTypes = AssetDownstreamUsageRules.OutboundMovementTypes.ToArray();

        // A negative Receipt/Opening movement is produced by reversing another inbound
        // document; it does not consume this source document's assets. Only operational
        // outbound movement types are conservative evidence of downstream usage until M6
        // can inspect per-asset custody and movement history.
        return await context.StockMovements
            .AsNoTracking()
            .AnyAsync(movement =>
                movement.WarehouseId == source.WarehouseId &&
                materialIds.Contains(movement.MaterialId) &&
                movement.DocumentId != source.Id &&
                movement.DocumentId != reversalDocumentId &&
                movement.PostedAtUtc >= source.PostedAtUtc.Value &&
                movement.QuantityDelta < 0 &&
                outboundMovementTypes.Contains(movement.MovementType),
                cancellationToken);
    }
}

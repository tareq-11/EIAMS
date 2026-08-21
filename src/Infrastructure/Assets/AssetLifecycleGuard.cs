using Application.Abstractions.Assets;
using Application.Abstractions.Data;
using Domain.Assets;
using Domain.Common;
using Domain.InventoryAdjustments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.Assets;

internal sealed class AssetLifecycleGuard(IApplicationDbContext context) : IAssetLifecycleGuard
{
    public async Task<Result> EnsureNotDisposedAsync(
        IEnumerable<Guid> assetIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = assetIds.Distinct().ToArray();
        Guid? disposedId = await context.AssetCurrentStatuses.AsNoTracking()
            .Where(item => ids.Contains(item.AssetId) && item.CurrentStatus == AssetCurrentStatus.Disposed)
            .Select(item => (Guid?)item.AssetId)
            .FirstOrDefaultAsync(cancellationToken);
        return disposedId.HasValue
            ? Result.Failure(DisposalErrors.AssetAlreadyDisposed(disposedId.Value))
            : Result.Success();
    }
}

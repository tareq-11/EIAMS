using SharedKernel;

namespace Application.Abstractions.Assets;

public interface IAssetLifecycleGuard
{
    Task<Result> EnsureNotDisposedAsync(IEnumerable<Guid> assetIds, CancellationToken cancellationToken);
}

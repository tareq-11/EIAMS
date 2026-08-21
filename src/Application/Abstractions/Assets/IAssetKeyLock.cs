namespace Application.Abstractions.Assets;

/// <summary>
/// Serializes asset state transitions inside the active posting transaction. Callers must provide
/// all affected asset identifiers; the implementation acquires them in deterministic order.
/// </summary>
public interface IAssetKeyLock
{
    Task AcquireAsync(IEnumerable<Guid> assetIds, CancellationToken cancellationToken);
}

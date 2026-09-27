using SharedKernel;

namespace Application.Abstractions.Ledger;

/// <summary>
/// Rebuilds the inventory-balance cache from immutable stock-movement facts. This maintenance
/// capability is intentionally not exposed as a public API operation.
/// </summary>
public interface IInventoryBalanceRebuilder
{
    Task<Result<int>> RebuildAllAsync(
        Guid rebuiltBy,
        DateTime rebuiltAtUtc,
        CancellationToken cancellationToken);
}

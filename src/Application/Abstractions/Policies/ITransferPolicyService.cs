using SharedKernel;

namespace Application.Abstractions.Policies;

/// <summary>Evaluates configurable policies that govern an atomic warehouse transfer.</summary>
public interface ITransferPolicyService
{
    /// <summary>Ensures a transfer between the two warehouses is allowed by current policy.</summary>
    Task<Result> EnsureTransferAllowedAsync(
        Guid sourceWarehouseId,
        Guid destinationWarehouseId,
        CancellationToken cancellationToken);
}

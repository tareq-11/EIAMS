using SharedKernel;

namespace Application.Abstractions.Ledger;

/// <summary>
/// The only writer allowed to touch StockMovement/InventoryBalance (D-INV-01/D-MOV-01). Locks the
/// affected balance keys in deterministic order, appends the movements, then recomputes each
/// balance from <c>SUM(quantity_delta)</c> - never trusting an in-memory increment - before updating
/// it (M3-PLAN.md §1.3). Must run inside an active <see cref="Data.IApplicationTransaction"/>.
/// </summary>
public interface IInventoryLedgerWriter
{
    Task<Result> AppendAsync(
        IReadOnlyCollection<MovementDraft> movements,
        Guid postedBy,
        DateTime postedAtUtc,
        CancellationToken cancellationToken);
}

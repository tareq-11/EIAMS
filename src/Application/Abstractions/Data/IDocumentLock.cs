using Domain.WarehouseDocuments;
using SharedKernel;

namespace Application.Abstractions.Data;

/// <summary>
/// Row-locks and reloads one WarehouseDocument for posting/reversal (<c>SELECT ... FOR UPDATE</c>).
/// Must be called inside an active <see cref="IApplicationTransaction"/> to have any locking effect.
/// Complements, but does not replace, the document's public optimistic-concurrency (RowVersion)
/// contract (M3-PLAN.md §1.3/§1.7).
/// </summary>
public interface IDocumentLock
{
    Task<Result<WarehouseDocument>> LockAsync(Guid documentId, CancellationToken cancellationToken);
}

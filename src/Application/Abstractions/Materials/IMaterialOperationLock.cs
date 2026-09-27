namespace Application.Abstractions.Materials;

/// <summary>
/// Row-locks the material catalog rows an operation is about to interpret, so a catalog edit and a
/// document transition that both read "is this material in operational use?" cannot both observe the
/// pre-transition state. Implemented with a transaction-scoped PostgreSQL advisory lock
/// (<c>pg_advisory_xact_lock</c>), so it must be called inside an active
/// <see cref="Data.IApplicationTransaction"/> to have any effect.
/// <para>
/// This complements, and does not replace, the public optimistic-concurrency contract: a material
/// still carries <c>CatalogVersion</c> as its EF concurrency token, and callers still send the
/// version they observed. The lock closes the read-check-write window inside the server; the version
/// closes the window between a client reading the catalog and sending its change.
/// </para>
/// </summary>
public interface IMaterialOperationLock
{
    Task AcquireAsync(IEnumerable<Guid> materialIds, CancellationToken cancellationToken);
}

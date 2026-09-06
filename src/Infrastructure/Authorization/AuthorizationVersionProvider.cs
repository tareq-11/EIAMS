using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Authorization;

/// <summary>
/// Reads the database-wide authorization version once per dependency-injection scope.
/// PostgreSQL increments the value in the same transaction as every authorization mutation,
/// so stale local cache entries are never addressed after a committed permission change.
/// </summary>
internal sealed class AuthorizationVersionProvider(ApplicationDbContext context)
{
    private Task<long>? currentVersion;

    internal Task<long> GetCurrentAsync(CancellationToken cancellationToken) =>
        currentVersion ??= context.Database
            .SqlQueryRaw<long>("SELECT version AS \"Value\" FROM public.authorization_versions WHERE id = 1")
            .SingleAsync(cancellationToken);
}

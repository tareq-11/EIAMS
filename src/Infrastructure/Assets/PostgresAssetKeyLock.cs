using Application.Abstractions.Assets;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Assets;

internal sealed class PostgresAssetKeyLock(ApplicationDbContext dbContext) : IAssetKeyLock
{
    public async Task AcquireAsync(IEnumerable<Guid> assetIds, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Asset-key locks require an active database transaction.");
        }

        string[] sortedKeys = assetIds
            .Distinct()
            .OrderBy(assetId => assetId)
            .Select(assetId => assetId.ToString())
            .ToArray();

        if (sortedKeys.Length == 0)
        {
            return;
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended(k, 0)) FROM unnest({sortedKeys}) AS k",
            cancellationToken);
    }
}

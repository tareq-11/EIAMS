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

        foreach (Guid assetId in assetIds.Distinct().OrderBy(assetId => assetId))
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({assetId.ToString()}, 0))",
                cancellationToken);
        }
    }
}

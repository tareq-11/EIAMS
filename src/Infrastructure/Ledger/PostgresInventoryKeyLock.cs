using Application.Abstractions.Ledger;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Ledger;

internal sealed class PostgresInventoryKeyLock(ApplicationDbContext dbContext) : IInventoryKeyLock
{
    public async Task AcquireAsync(
        IEnumerable<(Guid WarehouseId, Guid MaterialId)> keys,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Inventory-key locks require an active database transaction.");
        }

        string[] sortedKeys = keys
            .Distinct()
            .OrderBy(key => key.WarehouseId)
            .ThenBy(key => key.MaterialId)
            .Select(key => $"{key.WarehouseId}:{key.MaterialId}")
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

using Application.Abstractions.InventoryCounts;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.InventoryCounts;

internal sealed class PostgresWarehouseOperationLock(ApplicationDbContext dbContext)
    : IWarehouseOperationLock
{
    public async Task AcquireAsync(IEnumerable<Guid> warehouseIds, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Warehouse operation locks require an active database transaction.");
        }

        string[] sortedKeys = warehouseIds
            .Distinct()
            .OrderBy(id => id)
            .Select(id => $"warehouse:{id}")
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

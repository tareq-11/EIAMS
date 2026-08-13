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

        foreach ((Guid warehouseId, Guid materialId) in keys
                     .Distinct()
                     .OrderBy(key => key.WarehouseId)
                     .ThenBy(key => key.MaterialId))
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({warehouseId + ":" + materialId}, 0))",
                cancellationToken);
        }
    }
}

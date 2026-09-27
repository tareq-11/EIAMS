using Application.Abstractions.Materials;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Materials;

internal sealed class PostgresMaterialOperationLock(ApplicationDbContext dbContext) : IMaterialOperationLock
{
    public async Task AcquireAsync(IEnumerable<Guid> materialIds, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Material operation locks require an active database transaction.");
        }

        Guid[] sortedIds = materialIds.Distinct().OrderBy(id => id).ToArray();

        if (sortedIds.Length == 0)
        {
            return;
        }

        foreach (Guid materialId in sortedIds)
        {
            // Issue one lock statement per already-sorted id. Unlike relying on query-plan output
            // ordering, this makes the lock acquisition order explicit: each statement must
            // finish acquiring its transaction lock before the next key is requested.
            var materialIdParameter = new NpgsqlParameter("material_id", materialId)
            {
                DataTypeName = "uuid"
            };

            await dbContext.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(hashtextextended(@material_id::text, 0::bigint))",
                [materialIdParameter],
                cancellationToken);
        }
    }
}

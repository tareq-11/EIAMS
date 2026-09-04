using Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database;

internal sealed class PostgresApplicationLock(ApplicationDbContext dbContext) : IApplicationLock
{
    public async Task AcquireAsync(string resourceKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({resourceKey}, 0))",
            cancellationToken);
    }
}

using Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore.Storage;
using SharedKernel;

namespace Infrastructure.Database;

internal sealed class EfApplicationTransaction(ApplicationDbContext dbContext) : IApplicationTransaction
{
    public async Task<Result<TResult>> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<Result<TResult>>> action,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        Result<TResult> result = await action(cancellationToken);

        if (result.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);

            return result;
        }

        await transaction.CommitAsync(cancellationToken);

        return result;
    }
}

using SharedKernel;

namespace Application.Abstractions.Data;

/// <summary>
/// Runs <paramref name="action"/> inside one database transaction, committing only when it returns a
/// successful <see cref="Result"/> and rolling back (or letting the transaction dispose without a
/// commit) otherwise. Application code depends on this instead of EF's <c>DatabaseFacade</c>
/// directly, keeping transaction management out of the Application layer's vocabulary
/// (M3-PLAN.md §3.2).
/// </summary>
public interface IApplicationTransaction
{
    Task<Result<TResult>> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<Result<TResult>>> action,
        CancellationToken cancellationToken);
}

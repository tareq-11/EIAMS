namespace Application.Abstractions.Data;

/// <summary>
/// Acquires a transaction-scoped lock for a logical application resource. The caller must acquire
/// it from inside <see cref="IApplicationTransaction.ExecuteAsync{TResult}"/>.
/// </summary>
public interface IApplicationLock
{
    Task AcquireAsync(string resourceKey, CancellationToken cancellationToken);
}

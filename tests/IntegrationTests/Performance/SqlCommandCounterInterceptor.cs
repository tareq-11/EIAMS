using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IntegrationTests.Performance;

internal sealed class SqlCommandCounterInterceptor : DbCommandInterceptor
{
    private int commandCount;
    private readonly ConcurrentQueue<string> commandTexts = new();

    public int CommandCount => Volatile.Read(ref commandCount);

    public void Reset()
    {
        Interlocked.Exchange(ref commandCount, 0);
        commandTexts.Clear();
    }

    public IReadOnlyList<string> GetCommandTexts() => commandTexts.ToArray();

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Increment(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Increment(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        Increment(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Increment(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        Increment(command);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Increment(command);
        return ValueTask.FromResult(result);
    }

    private void Increment(DbCommand command)
    {
        commandTexts.Enqueue(command.CommandText);
        Interlocked.Increment(ref commandCount);
    }
}

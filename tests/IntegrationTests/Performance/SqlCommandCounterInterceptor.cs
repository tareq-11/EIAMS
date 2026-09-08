using System.Collections.Concurrent;
using System.Data.Common;
using System.Numerics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IntegrationTests.Performance;

internal sealed class SqlCommandCounterInterceptor : DbCommandInterceptor
{
    // Bucket zero is exactly zero. Positive bucket n represents (2^(n-2), 2^(n-1)] ticks,
    // with bucket 64 capped at Int64.MaxValue ticks. Quantiles report that upper bound.
    private const int HistogramBuckets = 65;
    private const int RetainedCommandTexts = 256;
    private readonly ConcurrentDictionary<Guid, long> commandGenerations = new();
    private readonly long[] histogram = new long[HistogramBuckets];
    private readonly ConcurrentQueue<string> commandTexts = new();
    private long generation;
    private long total;
    private long succeeded;
    private long failed;
    private long cancelled;
    private long totalDurationTicks;
    private long maxDurationTicks;

    public int CommandCount => checked((int)Math.Min(int.MaxValue, Interlocked.Read(ref total)));

    public void Reset()
    {
        Interlocked.Increment(ref generation);
        Interlocked.Exchange(ref total, 0);
        Interlocked.Exchange(ref succeeded, 0);
        Interlocked.Exchange(ref failed, 0);
        Interlocked.Exchange(ref cancelled, 0);
        Interlocked.Exchange(ref totalDurationTicks, 0);
        Interlocked.Exchange(ref maxDurationTicks, 0);
        Array.Clear(histogram);
        commandTexts.Clear();
    }

    /// <summary>Bounded, thread-safe online command-duration metrics; no SQL text, parameters, or IDs are included.</summary>
    public SqlCommandDurationSnapshot Snapshot()
    {
        long count = Interlocked.Read(ref total);
        long[] buckets = new long[HistogramBuckets];
        for (int index = 0; index < buckets.Length; index++)
        {
            buckets[index] = Interlocked.Read(ref histogram[index]);
        }
        return new SqlCommandDurationSnapshot(
            count, Interlocked.Read(ref succeeded), Interlocked.Read(ref failed), Interlocked.Read(ref cancelled),
            TimeSpan.FromTicks(Interlocked.Read(ref totalDurationTicks)).TotalMilliseconds,
            QuantileMilliseconds(buckets, count, .50), QuantileMilliseconds(buckets, count, .95),
            QuantileMilliseconds(buckets, count, .99), TimeSpan.FromTicks(Interlocked.Read(ref maxDurationTicks)).TotalMilliseconds);
    }

    public IReadOnlyList<string> GetCommandTexts() => commandTexts.ToArray();

    // Keeps collector behavior testable without manufacturing EF diagnostic event objects.
    internal void RecordForTest(TimeSpan duration, SqlCommandCompletionKind outcome) => Record(duration.Ticks, outcome);

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Started(command, eventData);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Started(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        Started(command, eventData);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Started(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        Started(command, eventData);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Started(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result) { Completed(eventData, SqlCommandCompletionKind.Succeeded); return result; }
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default) { Completed(eventData, SqlCommandCompletionKind.Succeeded); return ValueTask.FromResult(result); }
    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result) { Completed(eventData, SqlCommandCompletionKind.Succeeded); return result; }
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default) { Completed(eventData, SqlCommandCompletionKind.Succeeded); return ValueTask.FromResult(result); }
    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result) { Completed(eventData, SqlCommandCompletionKind.Succeeded); return result; }
    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default) { Completed(eventData, SqlCommandCompletionKind.Succeeded); return ValueTask.FromResult(result); }
    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData) => Completed(eventData, SqlCommandCompletionKind.Failed);
    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default) { Completed(eventData, SqlCommandCompletionKind.Failed); return Task.CompletedTask; }
    public override void CommandCanceled(DbCommand command, CommandEndEventData eventData) => Completed(eventData, SqlCommandCompletionKind.Cancelled);
    public override Task CommandCanceledAsync(DbCommand command, CommandEndEventData eventData, CancellationToken cancellationToken = default) { Completed(eventData, SqlCommandCompletionKind.Cancelled); return Task.CompletedTask; }

    private void Started(DbCommand command, CommandEventData eventData)
    {
        commandGenerations[eventData.CommandId] = Volatile.Read(ref generation);
        commandTexts.Enqueue(command.CommandText);
        while (commandTexts.Count > RetainedCommandTexts)
        {
            commandTexts.TryDequeue(out _);
        }
    }

    private void Completed(CommandEndEventData eventData, SqlCommandCompletionKind outcome)
    {
        if (!commandGenerations.TryRemove(eventData.CommandId, out long startedGeneration) || startedGeneration != Volatile.Read(ref generation))
        {
            return;
        }
        Record(eventData.Duration.Ticks, outcome);
    }

    private void Record(long durationTicks, SqlCommandCompletionKind outcome)
    {
        long ticks = Math.Max(0, durationTicks);
        Interlocked.Increment(ref total);
        if (outcome == SqlCommandCompletionKind.Succeeded)
        {
            Interlocked.Increment(ref succeeded);
        }
        else if (outcome == SqlCommandCompletionKind.Failed)
        {
            Interlocked.Increment(ref failed);
        }
        else
        {
            Interlocked.Increment(ref cancelled);
        }
        AddSaturating(ref totalDurationTicks, ticks);
        long oldMax;
        while ((oldMax = Interlocked.Read(ref maxDurationTicks)) < ticks && Interlocked.CompareExchange(ref maxDurationTicks, ticks, oldMax) != oldMax)
        {
            Thread.SpinWait(1);
        }
        Interlocked.Increment(ref histogram[Bucket(ticks)]);
    }

    private static int Bucket(long ticks)
    {
        if (ticks <= 0)
        {
            return 0;
        }

        return ticks == 1 ? 1 : BitOperations.Log2((ulong)(ticks - 1)) + 2;
    }

    private static long BucketUpperBoundTicks(int bucket) => bucket switch
    {
        0 => 0,
        1 => 1,
        HistogramBuckets - 1 => long.MaxValue,
        _ => 1L << (bucket - 1)
    };

    private static void AddSaturating(ref long target, long addend)
    {
        long current;
        long replacement;
        do
        {
            current = Interlocked.Read(ref target);
            replacement = current > long.MaxValue - addend ? long.MaxValue : current + addend;
        }
        while (Interlocked.CompareExchange(ref target, replacement, current) != current);
    }
    private static double? QuantileMilliseconds(long[] buckets, long count, double quantile)
    {
        if (count == 0)
        {
            return null;
        }
        long target = (long)Math.Ceiling(count * quantile);
        long seen = 0;
        for (int index = 0; index < buckets.Length; index++)
        {
            seen += buckets[index];
            if (seen >= target)
            {
                return TimeSpan.FromTicks(BucketUpperBoundTicks(index)).TotalMilliseconds;
            }
        }
        return null;
    }
}

internal sealed record SqlCommandDurationSnapshot(long Count, long Succeeded, long Failed, long Cancelled, double TotalDurationMs, double? ApproximateP50Ms, double? ApproximateP95Ms, double? ApproximateP99Ms, double MaxDurationMs);
internal enum SqlCommandCompletionKind { Succeeded, Failed, Cancelled }

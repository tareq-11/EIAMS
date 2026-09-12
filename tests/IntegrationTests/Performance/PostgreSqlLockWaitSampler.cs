using Npgsql;

namespace IntegrationTests.Performance;

/// <summary>
/// Test-only sampler of <c>pg_stat_activity.wait_event_type = 'Lock'</c>. PostgreSQL exposes
/// current wait state, not a cumulative per-request lock-wait duration. Consequently all values
/// produced here are sampled lock-wait occupancy, never request attribution or exact duration.
/// </summary>
internal sealed class PostgreSqlLockWaitSampler : IAsyncDisposable
{
    private const string CountWaitingSql = """
        SELECT count(*)
        FROM pg_stat_activity
        WHERE datname = current_database()
          AND pid <> @monitor_pid
          AND wait_event_type = 'Lock'
        """;

    private readonly string connectionString;
    private readonly TimeSpan interval;
    private readonly StopwatchMonotonicClock clock;
    private readonly SampledOccupancyIntegrator occupancyIntegrator = new();
    private readonly object lifecycleLock = new();
    private CancellationTokenSource? samplingCancellation;
    private Task? samplingTask;
    private int generation;
    private int disposed;
    private int available;
    private int failureCount;
    private int sampleCount;
    private int samplesWithLockWaits;
    private long totalWaitingSessionObservations;
    private int maxConcurrentWaitingSessions;

    internal PostgreSqlLockWaitSampler(string workloadConnectionString, TimeSpan? samplingInterval = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workloadConnectionString);
        interval = samplingInterval ?? TimeSpan.FromMilliseconds(25);
        if (interval < TimeSpan.FromMilliseconds(5) || interval > TimeSpan.FromSeconds(5))
        {
            throw new ArgumentOutOfRangeException(nameof(samplingInterval));
        }

        // A dedicated, non-pooled monitor cannot be mistaken for a workload session across phases.
        var builder = new NpgsqlConnectionStringBuilder(workloadConnectionString) { Pooling = false };
        connectionString = builder.ConnectionString;
        clock = new StopwatchMonotonicClock();
    }

    internal Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        lock (lifecycleLock)
        {
            if (samplingTask is not null)
            {
                throw new InvalidOperationException("The lock-wait sampler is already running.");
            }

            ResetCounters();
            int currentGeneration = ++generation;
            samplingCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            samplingTask = SampleAsync(currentGeneration, samplingCancellation.Token);
            return Task.CompletedTask;
        }
    }

    internal async Task<PostgreSqlLockWaitSnapshot> StopAsync()
    {
        Task? task;
        CancellationTokenSource? cancellation;
        lock (lifecycleLock)
        {
            task = samplingTask;
            cancellation = samplingCancellation;
            samplingTask = null;
            samplingCancellation = null;
        }

        if (cancellation is not null)
        {
            await cancellation.CancelAsync().ConfigureAwait(false);
        }
        try
        {
            if (task is not null)
            {
                await task.ConfigureAwait(false); // sampler handles operational failures internally.
            }
        }
        finally
        {
            cancellation?.Dispose();
        }
        return Snapshot();
    }

    internal PostgreSqlLockWaitSnapshot Snapshot() => new(
        IsAvailable: Volatile.Read(ref available) != 0,
        FailureCount: Volatile.Read(ref failureCount),
        SamplingIntervalMilliseconds: interval.TotalMilliseconds,
        SampleCount: Volatile.Read(ref sampleCount),
        SamplesWithLockWaits: Volatile.Read(ref samplesWithLockWaits),
        TotalWaitingSessionObservations: Interlocked.Read(ref totalWaitingSessionObservations),
        MaxConcurrentWaitingSessions: Volatile.Read(ref maxConcurrentWaitingSessions),
        ApproximateObservedWaitingSessionMilliseconds: occupancyIntegrator.ObservedWaitingSessionMilliseconds);

    private async Task SampleAsync(int taskGeneration, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var pidCommand = new NpgsqlCommand("SELECT pg_backend_pid()", connection);
            int monitorPid = (int)(await pidCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
            Volatile.Write(ref available, 1);

            while (!cancellationToken.IsCancellationRequested && taskGeneration == Volatile.Read(ref generation))
            {
                try
                {
                    await using var command = new NpgsqlCommand(CountWaitingSql, connection);
                    command.Parameters.AddWithValue("monitor_pid", monitorPid);
                    int waitingSessions = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);
                    Record(waitingSessions, clock.ElapsedTicks);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    Interlocked.Increment(ref failureCount);
                    Volatile.Write(ref available, 0);
                    break;
                }

                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch
        {
            Interlocked.Increment(ref failureCount);
            Volatile.Write(ref available, 0);
        }
        finally
        {
            // The sampled estimate ends when monitoring ends, including operational failure.
            // It must never extend stale occupancy to a later caller's StopAsync time.
            occupancyIntegrator.Complete(clock.ElapsedTicks);
        }
    }

    private void Record(int waitingSessions, long observedAtTicks)
    {
        occupancyIntegrator.Record(waitingSessions, observedAtTicks);
        Interlocked.Increment(ref sampleCount);
        if (waitingSessions <= 0)
        {
            return;
        }
        Interlocked.Increment(ref samplesWithLockWaits);
        Interlocked.Add(ref totalWaitingSessionObservations, waitingSessions);
        while (true)
        {
            int current = Volatile.Read(ref maxConcurrentWaitingSessions);
            if (waitingSessions <= current || Interlocked.CompareExchange(ref maxConcurrentWaitingSessions, waitingSessions, current) == current)
            {
                return;
            }
        }
    }

    private void ResetCounters()
    {
        Volatile.Write(ref available, 0); Volatile.Write(ref failureCount, 0); Volatile.Write(ref sampleCount, 0);
        Volatile.Write(ref samplesWithLockWaits, 0); Interlocked.Exchange(ref totalWaitingSessionObservations, 0);
        Volatile.Write(ref maxConcurrentWaitingSessions, 0);
        occupancyIntegrator.Reset();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            await StopAsync().ConfigureAwait(false);
        }
    }

    private sealed class StopwatchMonotonicClock
    {
        private readonly System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        internal long ElapsedTicks => stopwatch.Elapsed.Ticks;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
    }
}

internal sealed record PostgreSqlLockWaitSnapshot(
    bool IsAvailable,
    int FailureCount,
    double SamplingIntervalMilliseconds,
    int SampleCount,
    int SamplesWithLockWaits,
    long TotalWaitingSessionObservations,
    int MaxConcurrentWaitingSessions,
    double ApproximateObservedWaitingSessionMilliseconds);

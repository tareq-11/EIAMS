using System.Diagnostics.Metrics;

namespace IntegrationTests.Performance;

/// <summary>
/// Test-only reader for the low-cardinality Npgsql pool-state instruments. This intentionally does not
/// measure connection-acquisition latency: Npgsql 10.0.3 publishes no such duration instrument.
/// </summary>
internal sealed class NpgsqlPoolStateCollector : IDisposable
{
    internal const string MeterName = "Npgsql";
    internal const string ConnectionCountInstrument = "db.client.connection.count";
    internal const string ConnectionMaxInstrument = "db.client.connection.max";
    internal const string ConnectionTimeoutsInstrument = "db.client.connection.npgsql.timeouts";

    private readonly MeterListener listener = new();
    private readonly object gaugeCycleLock = new();
    private long idleConnections;
    private long usedConnections;
    private long maxConnections;
    private long poolTimeouts;
    private int idleConnectionsAvailable;
    private int usedConnectionsAvailable;
    private int maxConnectionsAvailable;
    private int timeoutsAvailable;
    private int disposed;

    public NpgsqlPoolStateCollector()
    {
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name != MeterName || !IsSupportedInstrument(instrument.Name))
            {
                return;
            }

            meterListener.EnableMeasurementEvents(instrument, this);
        };
        listener.SetMeasurementEventCallback<long>(static (instrument, value, tags, state) =>
            ((NpgsqlPoolStateCollector)state!).Record(instrument.Name, value, tags));
        listener.SetMeasurementEventCallback<int>(static (instrument, value, tags, state) =>
            ((NpgsqlPoolStateCollector)state!).Record(instrument.Name, value, tags));
        listener.SetMeasurementEventCallback<double>(static (instrument, value, tags, state) =>
            ((NpgsqlPoolStateCollector)state!).Record(instrument.Name, value, tags));
        listener.Start();
    }

    /// <summary>Starts a fresh benchmark phase; it does not alter the application's pool.</summary>
    public void Reset()
    {
        ThrowIfDisposed();
        ResetGaugeCycle();
        Interlocked.Exchange(ref poolTimeouts, 0);
        Volatile.Write(ref timeoutsAvailable, 0);
    }

    public NpgsqlPoolStateSnapshot Snapshot()
    {
        ThrowIfDisposed();
        // Each pull is a new aggregate cycle. A gauge reports one value per pool, so summing
        // inside the cycle produces a process-wide total rather than whichever pool reported last.
        bool idleAvailable;
        bool usedAvailable;
        bool maxAvailable;
        long idle;
        long used;
        long max;
        lock (gaugeCycleLock)
        {
            ResetGaugeCycleUnsafe();
            listener.RecordObservableInstruments();
            idleAvailable = idleConnectionsAvailable != 0;
            usedAvailable = usedConnectionsAvailable != 0;
            maxAvailable = maxConnectionsAvailable != 0;
            idle = idleConnections;
            used = usedConnections;
            max = maxConnections;
        }

        return new NpgsqlPoolStateSnapshot(
            idleAvailable && usedAvailable,
            maxAvailable,
            Volatile.Read(ref timeoutsAvailable) != 0,
            idle,
            used,
            max,
            Interlocked.Read(ref poolTimeouts));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            listener.Dispose();
        }
    }

    internal static bool IsSupportedInstrument(string name) => name is
        ConnectionCountInstrument or ConnectionMaxInstrument or ConnectionTimeoutsInstrument;

    private void Record(string instrumentName, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (Volatile.Read(ref disposed) != 0)
        {
            return;
        }

        // All provider tags (pool name, server address, port, etc.) are deliberately discarded.
        // The resulting gauges are process-wide aggregates; they cannot be attributed to an endpoint or pool.
        switch (instrumentName)
        {
            case ConnectionCountInstrument:
                string? state = GetConnectionState(tags);
                if (state == "idle")
                {
                    lock (gaugeCycleLock)
                    {
                        AddSaturating(ref idleConnections, value);
                        idleConnectionsAvailable = 1;
                    }
                }
                else if (state == "used")
                {
                    lock (gaugeCycleLock)
                    {
                        AddSaturating(ref usedConnections, value);
                        usedConnectionsAvailable = 1;
                    }
                }
                break;
            case ConnectionMaxInstrument:
                lock (gaugeCycleLock)
                {
                    AddSaturating(ref maxConnections, value);
                    maxConnectionsAvailable = 1;
                }
                break;
            case ConnectionTimeoutsInstrument:
                Interlocked.Add(ref poolTimeouts, Math.Max(0, value));
                Volatile.Write(ref timeoutsAvailable, 1);
                break;
        }
    }

    private void Record(string instrumentName, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (double.IsNaN(value) || value <= 0)
        {
            Record(instrumentName, 0, tags);
            return;
        }

        Record(instrumentName, value >= long.MaxValue ? long.MaxValue : (long)value, tags);
    }

    private static string? GetConnectionState(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (KeyValuePair<string, object?> tag in tags)
        {
            if ((tag.Key == "state" || tag.Key == "db.client.connection.state") &&
                tag.Value is string state && (state == "idle" || state == "used"))
            {
                return state;
            }
        }
        return null;
    }

    private void ResetGaugeCycle()
    {
        lock (gaugeCycleLock)
        {
            ResetGaugeCycleUnsafe();
        }
    }

    private void ResetGaugeCycleUnsafe()
    {
        idleConnections = 0;
        usedConnections = 0;
        maxConnections = 0;
        idleConnectionsAvailable = 0;
        usedConnectionsAvailable = 0;
        maxConnectionsAvailable = 0;
    }

    private static void AddSaturating(ref long target, long value)
    {
        long nonNegative = Math.Max(0, value);
        target = target > long.MaxValue - nonNegative ? long.MaxValue : target + nonNegative;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
    }
}

internal sealed record NpgsqlPoolStateSnapshot(
    bool ConnectionCountAvailable,
    bool ConnectionMaxAvailable,
    bool ConnectionTimeoutsAvailable,
    long IdleConnections,
    long UsedConnections,
    long MaxConnections,
    long PoolTimeouts);

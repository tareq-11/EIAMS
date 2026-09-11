using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace IntegrationTests.Performance;

public sealed class NpgsqlPoolStateCollectorTests
{
    [Fact]
    public void Snapshot_SubscribesToOfficialLowCardinalityPoolInstrumentsAndDropsProviderTags()
    {
        using var collector = new NpgsqlPoolStateCollector();
        using var meter = new Meter(NpgsqlPoolStateCollector.MeterName, "test");
        Counter<long> timeouts = meter.CreateCounter<long>(NpgsqlPoolStateCollector.ConnectionTimeoutsInstrument);
        _ = meter.CreateObservableGauge(
            NpgsqlPoolStateCollector.ConnectionCountInstrument,
            () => new Measurement<long>[]
            {
                new(3, new TagList { { "state", "idle" }, { "server.address", "pool-a-host" }, { "db.client.connection.pool.name", "pool-a" } }),
                new(2, new TagList { { "state", "used" }, { "server.address", "pool-a-host" }, { "db.client.connection.pool.name", "pool-a" } }),
                new(7, new TagList { { "state", "idle" }, { "server.address", "pool-b-host" }, { "db.client.connection.pool.name", "pool-b" } }),
                new(5, new TagList { { "state", "used" }, { "server.address", "pool-b-host" }, { "db.client.connection.pool.name", "pool-b" } })
            });
        _ = meter.CreateObservableGauge(
            NpgsqlPoolStateCollector.ConnectionMaxInstrument,
            () => new Measurement<long>[]
            {
                new(5, new TagList { { "server.address", "pool-a-host" }, { "db.client.connection.pool.name", "pool-a" } }),
                new(10, new TagList { { "server.address", "pool-b-host" }, { "db.client.connection.pool.name", "pool-b" } })
            });

        timeouts.Add(2, new TagList { { "server.address", "must-not-retain" } });
        NpgsqlPoolStateSnapshot firstSnapshot = collector.Snapshot();
        NpgsqlPoolStateSnapshot secondSnapshot = collector.Snapshot();

        // Gauge measurements from both pools are summed once per snapshot; the timeout counter remains phase-wide.
        firstSnapshot.ShouldBe(new NpgsqlPoolStateSnapshot(true, true, true, 10, 7, 15, 2));
        secondSnapshot.ShouldBe(firstSnapshot);
    }

    [Fact]
    public void ResetAndConcurrentRecording_ArePhaseBoundedAndThreadSafe()
    {
        using var collector = new NpgsqlPoolStateCollector();
        using var meter = new Meter(NpgsqlPoolStateCollector.MeterName, "test");
        Counter<long> timeouts = meter.CreateCounter<long>(NpgsqlPoolStateCollector.ConnectionTimeoutsInstrument);

        Parallel.For(0, 512, _ => timeouts.Add(1));
        collector.Snapshot().PoolTimeouts.ShouldBe(512);

        collector.Reset();
        collector.Snapshot().PoolTimeouts.ShouldBe(0);
    }

    [Fact]
    public void PublishedTimeoutCounterWithoutMeasurements_IsAvailableAtZeroAndSurvivesReset()
    {
        using var collector = new NpgsqlPoolStateCollector();
        using var meter = new Meter(NpgsqlPoolStateCollector.MeterName, "test");
        _ = meter.CreateCounter<long>(NpgsqlPoolStateCollector.ConnectionTimeoutsInstrument);

        NpgsqlPoolStateSnapshot beforeReset = collector.Snapshot();
        beforeReset.ConnectionTimeoutsAvailable.ShouldBeTrue();
        beforeReset.PoolTimeouts.ShouldBe(0);

        collector.Reset();
        NpgsqlPoolStateSnapshot afterReset = collector.Snapshot();
        afterReset.ConnectionTimeoutsAvailable.ShouldBeTrue();
        afterReset.PoolTimeouts.ShouldBe(0);
    }

    [Fact]
    public void Dispose_UnsubscribesAndPreventsFurtherSnapshots()
    {
        var collector = new NpgsqlPoolStateCollector();
        using var meter = new Meter(NpgsqlPoolStateCollector.MeterName, "test");
        Counter<long> timeouts = meter.CreateCounter<long>(NpgsqlPoolStateCollector.ConnectionTimeoutsInstrument);

        collector.Dispose();
        timeouts.Add(1);

        Should.Throw<ObjectDisposedException>(() => collector.Snapshot());
    }

    [Fact]
    public void Metadata_UsesNpgsql103OfficialPoolStateNamesAndDoesNotClaimWaitDuration()
    {
        NpgsqlPoolStateCollector.MeterName.ShouldBe("Npgsql");
        NpgsqlPoolStateCollector.ConnectionCountInstrument.ShouldBe("db.client.connection.count");
        NpgsqlPoolStateCollector.ConnectionMaxInstrument.ShouldBe("db.client.connection.max");
        NpgsqlPoolStateCollector.ConnectionTimeoutsInstrument.ShouldBe("db.client.connection.npgsql.timeouts");
        NpgsqlPoolStateCollector.IsSupportedInstrument("db.client.connection.npgsql.pending_requests").ShouldBeFalse();
    }
}

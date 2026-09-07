using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Infrastructure.Database;

namespace Application.UnitTests.Performance;

public sealed class CacheInvalidationMetricsTests
{
    [Fact]
    public void Record_Should_ExposeOnlyBoundedOperationalTags()
    {
        var measurements = new ConcurrentBag<Measurement>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == CacheInvalidationMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
            measurements.Add(new Measurement(instrument.Name, tags.ToArray())));
        listener.SetMeasurementEventCallback<int>((instrument, _, tags, _) =>
            measurements.Add(new Measurement(instrument.Name, tags.ToArray())));
        listener.Start();

        long timestamp = Stopwatch.GetTimestamp();
        CacheInvalidationMetrics.Record(
            "post_commit",
            tagCount: 2,
            timestamp,
            succeeded: true,
            pendingStartedTimestamp: timestamp);

        measurements.Select(item => item.InstrumentName).ShouldBe(
            [
                "cache.invalidation.pending_lag",
                "cache.invalidation.tag_count",
                "cache.invalidation.duration"
            ],
            ignoreOrder: true);
        measurements.SelectMany(item => item.Tags)
            .Select(tag => tag.Key)
            .Distinct()
            .ShouldBe(["phase", "succeeded"], ignoreOrder: true);
        measurements.SelectMany(item => item.Tags)
            .All(tag => tag.Value is "post_commit" or true)
            .ShouldBeTrue();
    }

    private sealed record Measurement(
        string InstrumentName,
        KeyValuePair<string, object?>[] Tags);
}

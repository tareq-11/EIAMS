using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Infrastructure.Authorization;

namespace Application.UnitTests.Performance;

public sealed class AuthorizationCacheCoalescingMetricsTests
{
    [Fact]
    public void ConcurrentLookup_ShouldRecordFactoryExecutionAndCoalescingWithFixedTagsOnly()
    {
        // Arrange
        var measurements = new ConcurrentBag<Measurement>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == AuthorizationCacheMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            measurements.Add(new Measurement(instrument.Name, measurement, tags.ToArray())));
        listener.Start();

        var tracker = new AuthorizationCacheCoalescingTracker();
        string cacheKey = $"auth:user-grants:{Guid.NewGuid()}:v1";
        KeyValuePair<string, object?>[] expectedTags = [new("key_type", "user_grants")];
        AuthorizationCacheCoalescingTracker.CacheLookup factoryLookup = tracker.Begin(cacheKey, "user_grants");
        AuthorizationCacheCoalescingTracker.CacheLookup waitingLookup = tracker.Begin(cacheKey, "user_grants");

        // Act
        tracker.RecordFactoryExecution(factoryLookup);
        tracker.Complete(waitingLookup);
        tracker.Complete(factoryLookup);

        // Assert
        measurements.Any(item =>
            item.InstrumentName == "authorization.cache.factory_executions" &&
            item.Value == 1 &&
            item.Tags.SequenceEqual(expectedTags)).ShouldBeTrue();
        measurements.Any(item =>
            item.InstrumentName == "authorization.cache.coalesced_requests" &&
            item.Value == 1 &&
            item.Tags.SequenceEqual(expectedTags)).ShouldBeTrue();
        measurements.SelectMany(item => item.Tags)
            .Select(tag => tag.Value)
            .All(value => value is "user_grants")
            .ShouldBeTrue();
    }

    private sealed record Measurement(
        string InstrumentName,
        long Value,
        KeyValuePair<string, object?>[] Tags);
}

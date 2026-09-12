using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Application.Users.Refresh;

namespace Application.UnitTests.Users;

public sealed class RefreshSecurityMetricsTests
{
    [Fact]
    public void ReplayDetected_RecordsOnlyFixedEventTypeAndOutcome()
    {
        var measurements = new ConcurrentBag<KeyValuePair<string, object?>[]>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == RefreshSecurityMetrics.MeterName &&
                instrument.Name == RefreshSecurityMetrics.InstrumentName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) => measurements.Add(tags.ToArray()));
        listener.Start();

        RefreshSecurityMetrics.ReplayDetected();

        measurements.Any(tags => tags.SequenceEqual(
            [new KeyValuePair<string, object?>("event_type", "refresh_replay"),
             new KeyValuePair<string, object?>("outcome", "detected")])).ShouldBeTrue();
        measurements.SelectMany(tags => tags).Select(tag => tag.Key).Distinct()
            .ShouldBe(["event_type", "outcome"], ignoreOrder: true);
        measurements.SelectMany(tags => tags).Select(tag => tag.Value).OfType<string>()
            .ShouldAllBe(value => IsSafeValue(value));
    }

    private static bool IsSafeValue(string value) => !value.Contains('@') && !Guid.TryParse(value, out _);
}

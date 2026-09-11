using System.Diagnostics.Metrics;

namespace Application.Users.Refresh;

internal static class RefreshSecurityMetrics
{
    internal const string MeterName = "CleanArchitecture.Application.Authentication";
    internal const string InstrumentName = "security.refresh.events";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Events = Meter.CreateCounter<long>(InstrumentName);

    internal static void ReplayDetected() => Events.Add(1, new KeyValuePair<string, object?>("event_type", "refresh_replay"), new KeyValuePair<string, object?>("outcome", "detected"));
}

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Application.Users.Login;

internal static class LoginMetrics
{
    internal const string MeterName = "CleanArchitecture.Application.Authentication";
    internal const string InstrumentName = "authentication.login.phase.duration";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Histogram<double> PhaseDuration = Meter.CreateHistogram<double>(
        InstrumentName,
        "ms",
        "Time spent in each fixed login phase. No user identifiers are recorded.");

    internal static long Start() => Stopwatch.GetTimestamp();

    internal static void Record(string phase, long startedAt) =>
        PhaseDuration.Record(
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
            new KeyValuePair<string, object?>("auth.phase", phase));
}

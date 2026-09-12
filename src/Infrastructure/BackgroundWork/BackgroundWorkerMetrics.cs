using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Infrastructure.BackgroundWork;

/// <summary>
/// Low-cardinality operational signals for non-critical background work.
/// Worker failures are deliberately observable without making the API readiness endpoint fail.
/// </summary>
internal static class BackgroundWorkerMetrics
{
    internal const string MeterName = "CleanArchitecture.Infrastructure.BackgroundWork";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Cycles = Meter.CreateCounter<long>(
        "background_worker.cycles",
        unit: "{cycle}",
        description: "Completed background worker cycles by worker and outcome.");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "background_worker.cycle.duration",
        unit: "ms",
        description: "Duration of a background worker cycle.");
    private static readonly Counter<long> Items = Meter.CreateCounter<long>(
        "background_worker.items",
        unit: "{item}",
        description: "Items handled by a background worker cycle.");

    internal static long Start() => Stopwatch.GetTimestamp();

    internal static void Record(string worker, string outcome, long startedTimestamp, int itemCount = 0)
    {
        KeyValuePair<string, object?> workerTag = new("worker", worker);
        KeyValuePair<string, object?> outcomeTag = new("outcome", outcome);

        Cycles.Add(1, workerTag, outcomeTag);
        Duration.Record(Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds, workerTag, outcomeTag);

        if (itemCount > 0)
        {
            Items.Add(itemCount, workerTag, outcomeTag);
        }
    }
}

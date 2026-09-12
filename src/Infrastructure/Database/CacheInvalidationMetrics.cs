using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Infrastructure.Database;

internal static class CacheInvalidationMetrics
{
    internal const string MeterName = "CleanArchitecture.Infrastructure.Cache";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "cache.invalidation.duration",
        unit: "ms",
        description: "Time spent invalidating cache tags.");
    private static readonly Histogram<double> PendingLag = Meter.CreateHistogram<double>(
        "cache.invalidation.pending_lag",
        unit: "ms",
        description: "Time between a transactional save and post-commit cache invalidation.");
    private static readonly Histogram<int> TagCount = Meter.CreateHistogram<int>(
        "cache.invalidation.tag_count",
        unit: "{tag}",
        description: "Number of distinct tags in one cache invalidation operation.");

    internal static void Record(
        string phase,
        int tagCount,
        long startedTimestamp,
        bool succeeded,
        long? pendingStartedTimestamp = null)
    {
        KeyValuePair<string, object?> phaseTag = new("phase", phase);
        KeyValuePair<string, object?> outcomeTag = new("succeeded", succeeded);

        Duration.Record(
            Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds,
            phaseTag,
            outcomeTag);
        TagCount.Record(tagCount, phaseTag);

        if (pendingStartedTimestamp.HasValue)
        {
            PendingLag.Record(
                Stopwatch.GetElapsedTime(pendingStartedTimestamp.Value).TotalMilliseconds,
                outcomeTag);
        }
    }
}

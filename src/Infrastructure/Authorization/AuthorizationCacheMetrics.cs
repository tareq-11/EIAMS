using System.Diagnostics.Metrics;

namespace Infrastructure.Authorization;

internal static class AuthorizationCacheMetrics
{
    internal const string MeterName = "CleanArchitecture.Infrastructure.Authorization";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> CacheHits = Meter.CreateCounter<long>(
        "authorization.cache.hits",
        unit: "{entry}",
        description: "Authorization cache lookups served without invoking the value factory.");
    private static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>(
        "authorization.cache.misses",
        unit: "{entry}",
        description: "Authorization cache lookups that invoked the value factory.");

    internal static void RecordHit(string keyType) =>
        CacheHits.Add(1, new KeyValuePair<string, object?>("key_type", keyType));

    internal static void RecordMiss(string keyType) =>
        CacheMisses.Add(1, new KeyValuePair<string, object?>("key_type", keyType));
}

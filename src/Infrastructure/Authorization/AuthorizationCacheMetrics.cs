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
    private static readonly Counter<long> FactoryExecutions = Meter.CreateCounter<long>(
        "authorization.cache.factory_executions",
        unit: "{execution}",
        description: "Authorization cache value-factory executions.");
    private static readonly Counter<long> CoalescedRequests = Meter.CreateCounter<long>(
        "authorization.cache.coalesced_requests",
        unit: "{request}",
        description: "Authorization cache lookups that joined an in-flight value factory.");

    internal static void RecordHit(string keyType) =>
        CacheHits.Add(1, new KeyValuePair<string, object?>("key_type", keyType));

    internal static void RecordMiss(string keyType) =>
        CacheMisses.Add(1, new KeyValuePair<string, object?>("key_type", keyType));

    internal static void RecordFactoryExecution(string keyType) =>
        FactoryExecutions.Add(1, new KeyValuePair<string, object?>("key_type", keyType));

    internal static void RecordCoalesced(string keyType) =>
        CoalescedRequests.Add(1, new KeyValuePair<string, object?>("key_type", keyType));
}

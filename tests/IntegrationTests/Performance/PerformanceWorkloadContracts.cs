namespace IntegrationTests.Performance;

/// <summary>
/// Stable, low-cardinality labels used to keep expected-traffic performance evidence
/// separate from functional rate-limit abuse checks.
/// </summary>
internal static class PerformanceWorkloadContracts
{
    internal const string NormalExpectedTraffic = "NormalExpectedTraffic";
    internal const string Abuse = "Abuse";
    internal const string SecurityCategory = "Security";

    internal static void EnsureNormalExpectedTrafficHasNoFailures(ApiLoadTestHttpMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        if (metrics.UnexpectedHttp + metrics.RateLimited + metrics.TimeoutOrCancellation + metrics.TransportFailures != 0)
        {
            throw new InvalidOperationException(
                $"{NormalExpectedTraffic} must have no HTTP failures; " +
                $"unexpected_http={metrics.UnexpectedHttp}, http_429={metrics.RateLimited}, " +
                $"timeouts_or_cancellations={metrics.TimeoutOrCancellation}, " +
                $"transport_failures={metrics.TransportFailures}.");
        }
    }
}

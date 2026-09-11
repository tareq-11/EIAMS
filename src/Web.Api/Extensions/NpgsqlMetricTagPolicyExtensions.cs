using OpenTelemetry.Metrics;

namespace Web.Api.Extensions;

/// <summary>Applies the only Npgsql metric dimensions permitted for export.</summary>
internal static class NpgsqlMetricTagPolicyExtensions
{
    internal const string MeterName = "Npgsql";
    internal const string ConnectionCount = "db.client.connection.count";
    internal const string ConnectionMax = "db.client.connection.max";
    internal const string ConnectionCreateTime = "db.client.connection.npgsql.create_time";
    internal const string ConnectionPendingRequests = "db.client.connection.npgsql.pending_requests";
    internal const string ConnectionTimeouts = "db.client.connection.npgsql.timeouts";
    internal const string OperationDuration = "db.client.operation.duration";
    internal const string OperationFailed = "db.client.operation.failed";
    internal const string OperationBytesRead = "db.client.operation.npgsql.bytes_read";
    internal const string OperationBytesWritten = "db.client.operation.npgsql.bytes_written";
    internal const string OperationExecuting = "db.client.operation.npgsql.executing";
    internal const string OperationPreparedRatio = "db.client.operation.npgsql.prepared_ratio";

    internal static IReadOnlyList<string> SupportedInstruments { get; } =
    [
        ConnectionCount, ConnectionMax, ConnectionCreateTime, ConnectionPendingRequests,
        ConnectionTimeouts, OperationDuration, OperationFailed, OperationBytesRead,
        OperationBytesWritten, OperationExecuting, OperationPreparedRatio
    ];

    internal static MeterProviderBuilder ApplyNpgsqlMetricTagPolicy(this MeterProviderBuilder metrics)
    {
        foreach (string instrument in SupportedInstruments)
        {
            metrics.AddView(instrument, new MetricStreamConfiguration
            {
                TagKeys = instrument == ConnectionCount ? ["db.client.connection.state"] : []
            });
        }

        return metrics;
    }

    internal static IReadOnlyList<string> AllowedTagKeys(string instrumentName) => instrumentName switch
    {
        ConnectionCount => ["db.client.connection.state"],
        ConnectionMax or ConnectionCreateTime or ConnectionPendingRequests or ConnectionTimeouts or
            OperationDuration or OperationFailed or OperationBytesRead or OperationBytesWritten or
            OperationExecuting or OperationPreparedRatio => [],
        _ => throw new ArgumentOutOfRangeException(nameof(instrumentName))
    };
}

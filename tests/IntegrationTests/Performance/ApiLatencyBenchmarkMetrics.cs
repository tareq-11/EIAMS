using System.Diagnostics;
using System.Net.Http.Headers;

namespace IntegrationTests.Performance;

public enum ApiBenchmarkResponseClassification
{
    ExpectedResponse,
    UnexpectedHttpError,
    RateLimited,
    TimeoutOrCancellation,
    TransportFailure
}

internal sealed record ApiBenchmarkSample(
    double? ElapsedMs,
    int ResponsePayloadBytes,
    ApiBenchmarkResponseClassification Classification,
    int? HttpStatusCode,
    double? RetryAfterSeconds);

internal sealed record ApiBenchmarkProcessSnapshot(
    double CpuTimeMs,
    long ResidentSetBytes,
    long AllocatedBytes,
    int Gen0CollectionCount,
    int Gen1CollectionCount,
    int Gen2CollectionCount,
    int? ThreadPoolAvailableWorkerThreads,
    int? ThreadPoolAvailableCompletionPortThreads,
    long? ThreadPoolPendingWorkItemCount);

internal sealed record ApiBenchmarkProcessDelta(
    double CpuTimeMs,
    long ResidentSetBytes,
    long AllocatedBytes,
    int Gen0CollectionCount,
    int Gen1CollectionCount,
    int Gen2CollectionCount,
    int? ThreadPoolAvailableWorkerThreads,
    int? ThreadPoolAvailableCompletionPortThreads,
    long? ThreadPoolPendingWorkItemCount);

internal sealed record ApiLatencyWindowMetrics(
    int SampleCount,
    int SuccessfulSampleCount,
    int CompletedSampleCount,
    double? P50Ms,
    double? P95Ms,
    double? P99Ms,
    double? AverageMs,
    double AttemptedThroughputRequestsPerSecond,
    double CompletedThroughputRequestsPerSecond,
    double SuccessfulThroughputRequestsPerSecond,
    long CompletedResponsePayloadBytes,
    double? AverageResponsePayloadBytesPerCompletedResponse,
    int UnexpectedHttpErrorCount,
    int TimeoutOrCancellationCount,
    int TransportFailureCount,
    int RateLimitedCount,
    ApiBenchmarkRetryAfterSummary RetryAfter,
    ApiBenchmarkProcessSnapshot ProcessBeforeWindow,
    ApiBenchmarkProcessSnapshot ProcessAfterWindow,
    ApiBenchmarkProcessDelta ProcessDelta);

internal sealed record ApiBenchmarkRetryAfterSummary(
    int Count,
    double? MinimumSeconds,
    double? MaximumSeconds,
    double? AverageSeconds);

internal static class ApiLatencyBenchmarkMetrics
{
    internal static ApiBenchmarkProcessSnapshot CaptureProcessSnapshot()
    {
        using var process = Process.GetCurrentProcess();
        ThreadPool.GetAvailableThreads(out int availableWorkerThreads, out int availableCompletionPortThreads);
        return new ApiBenchmarkProcessSnapshot(
            process.TotalProcessorTime.TotalMilliseconds,
            process.WorkingSet64,
            GC.GetTotalAllocatedBytes(precise: false),
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
            availableWorkerThreads,
            availableCompletionPortThreads,
            ThreadPool.PendingWorkItemCount);
    }

    internal static ApiBenchmarkResponseClassification Classify(
        int? statusCode,
        int expectedStatusCode,
        bool timedOutOrCancelled)
    {
        if (timedOutOrCancelled)
        {
            return ApiBenchmarkResponseClassification.TimeoutOrCancellation;
        }

        if (statusCode is null)
        {
            return ApiBenchmarkResponseClassification.TransportFailure;
        }

        if (statusCode == 429)
        {
            return ApiBenchmarkResponseClassification.RateLimited;
        }

        return statusCode == expectedStatusCode
            ? ApiBenchmarkResponseClassification.ExpectedResponse
            : ApiBenchmarkResponseClassification.UnexpectedHttpError;
    }

    internal static double? ParseRetryAfterSeconds(
        RetryConditionHeaderValue? retryAfter,
        DateTimeOffset nowUtc)
    {
        if (retryAfter?.Delta is TimeSpan delay)
        {
            return Math.Max(0, delay.TotalSeconds);
        }

        return retryAfter?.Date is DateTimeOffset retryAt
            ? Math.Max(0, (retryAt - nowUtc).TotalSeconds)
            : null;
    }

    internal static ApiLatencyWindowMetrics Calculate(
        IReadOnlyList<ApiBenchmarkSample> samples,
        TimeSpan measurementWindow,
        ApiBenchmarkProcessSnapshot processBeforeWindow,
        ApiBenchmarkProcessSnapshot processAfterWindow)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentOutOfRangeException.ThrowIfLessThan(measurementWindow, TimeSpan.Zero);
        ValidateSamples(samples);

        double[] successfulLatencies = samples
            .Where(sample => sample.Classification == ApiBenchmarkResponseClassification.ExpectedResponse)
            .Select(sample => sample.ElapsedMs)
            .Where(elapsedMs => elapsedMs is not null)
            .Select(elapsedMs => elapsedMs!.Value)
            .Order()
            .ToArray();
        int successfulSampleCount = samples.Count(sample =>
            sample.Classification == ApiBenchmarkResponseClassification.ExpectedResponse);
        int completedSampleCount = samples.Count(sample => sample.HttpStatusCode is not null);
        long completedResponsePayloadBytes = samples
            .Where(sample => sample.HttpStatusCode is not null)
            .Sum(sample => (long)sample.ResponsePayloadBytes);
        double windowSeconds = measurementWindow.TotalSeconds;
        double[] retryAfterSeconds = samples
            .Where(sample => sample.Classification == ApiBenchmarkResponseClassification.RateLimited)
            .Where(sample => sample.RetryAfterSeconds is not null)
            .Select(sample => sample.RetryAfterSeconds!.Value)
            .ToArray();

        return new ApiLatencyWindowMetrics(
            samples.Count,
            successfulSampleCount,
            completedSampleCount,
            Percentile(successfulLatencies, 0.50),
            Percentile(successfulLatencies, 0.95),
            Percentile(successfulLatencies, 0.99),
            successfulSampleCount == 0 ? null : successfulLatencies.Average(),
            windowSeconds <= 0 ? 0 : samples.Count / windowSeconds,
            windowSeconds <= 0 ? 0 : completedSampleCount / windowSeconds,
            windowSeconds <= 0 ? 0 : successfulSampleCount / windowSeconds,
            completedResponsePayloadBytes,
            completedSampleCount == 0 ? null : completedResponsePayloadBytes / (double)completedSampleCount,
            samples.Count(sample => sample.Classification == ApiBenchmarkResponseClassification.UnexpectedHttpError),
            samples.Count(sample => sample.Classification == ApiBenchmarkResponseClassification.TimeoutOrCancellation),
            samples.Count(sample => sample.Classification == ApiBenchmarkResponseClassification.TransportFailure),
            samples.Count(sample => sample.Classification == ApiBenchmarkResponseClassification.RateLimited),
            new ApiBenchmarkRetryAfterSummary(
                retryAfterSeconds.Length,
                retryAfterSeconds.Length == 0 ? null : retryAfterSeconds.Min(),
                retryAfterSeconds.Length == 0 ? null : retryAfterSeconds.Max(),
                retryAfterSeconds.Length == 0 ? null : retryAfterSeconds.Average()),
            processBeforeWindow,
            processAfterWindow,
            CalculateDelta(processBeforeWindow, processAfterWindow));
    }

    internal static double? Percentile(IReadOnlyList<double> orderedValues, double percentile)
    {
        if (orderedValues.Count == 0)
        {
            return null;
        }

        if (percentile is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(percentile));
        }

        int index = Math.Max(0, (int)Math.Ceiling(orderedValues.Count * percentile) - 1);
        return orderedValues[index];
    }

    internal static ApiBenchmarkProcessDelta CalculateDelta(
        ApiBenchmarkProcessSnapshot before,
        ApiBenchmarkProcessSnapshot after) =>
        new(
            after.CpuTimeMs - before.CpuTimeMs,
            after.ResidentSetBytes - before.ResidentSetBytes,
            after.AllocatedBytes - before.AllocatedBytes,
            after.Gen0CollectionCount - before.Gen0CollectionCount,
            after.Gen1CollectionCount - before.Gen1CollectionCount,
            after.Gen2CollectionCount - before.Gen2CollectionCount,
            Difference(after.ThreadPoolAvailableWorkerThreads, before.ThreadPoolAvailableWorkerThreads),
            Difference(after.ThreadPoolAvailableCompletionPortThreads, before.ThreadPoolAvailableCompletionPortThreads),
            Difference(after.ThreadPoolPendingWorkItemCount, before.ThreadPoolPendingWorkItemCount));

    private static int? Difference(int? after, int? before) =>
        after is null || before is null ? null : after.Value - before.Value;

    private static long? Difference(long? after, long? before) =>
        after is null || before is null ? null : after.Value - before.Value;

    private static void ValidateSamples(IReadOnlyList<ApiBenchmarkSample> samples)
    {
        foreach (ApiBenchmarkSample sample in samples)
        {
            if (sample.ElapsedMs is < 0 || sample.ResponsePayloadBytes < 0 || sample.RetryAfterSeconds is < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(samples), "Sample measurements cannot be negative.");
            }

            switch (sample.Classification)
            {
                case ApiBenchmarkResponseClassification.ExpectedResponse when
                    sample.HttpStatusCode is null || sample.ElapsedMs is null:
                    throw new ArgumentException(
                        "An expected HTTP response requires a status code and elapsed time.",
                        nameof(samples));
                case ApiBenchmarkResponseClassification.UnexpectedHttpError or
                    ApiBenchmarkResponseClassification.RateLimited when sample.HttpStatusCode is null:
                    throw new ArgumentException(
                        "An HTTP response classification requires a status code.",
                        nameof(samples));
                case ApiBenchmarkResponseClassification.RateLimited when sample.HttpStatusCode != 429:
                    throw new ArgumentException(
                        "A rate-limited response must have HTTP status 429.",
                        nameof(samples));
                case ApiBenchmarkResponseClassification.TimeoutOrCancellation or
                    ApiBenchmarkResponseClassification.TransportFailure when
                    sample.HttpStatusCode is not null ||
                    sample.ResponsePayloadBytes != 0 ||
                    sample.RetryAfterSeconds is not null:
                    throw new ArgumentException(
                        "Timeout and transport failures cannot have HTTP response data.",
                        nameof(samples));
                case not ApiBenchmarkResponseClassification.RateLimited when sample.RetryAfterSeconds is not null:
                    throw new ArgumentException(
                        "Retry-After is only valid for rate-limited responses.",
                        nameof(samples));
            }
        }
    }
}

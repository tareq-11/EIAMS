using System.Net.Http.Headers;

namespace IntegrationTests.Performance;

public sealed class ApiLatencyBenchmarkMetricsTests
{
    [Fact]
    public void Calculate_ShouldUseMeasurementWindowAndSeparateFailures()
    {
        ApiBenchmarkProcessSnapshot before = new(100, 1_000, 2_000, 3, 4, 5, 6, 7, 8);
        ApiBenchmarkProcessSnapshot after = new(125, 1_250, 2_500, 5, 5, 6, 4, 8, 11);
        ApiBenchmarkSample[] samples =
        [
            new(10, 100, ApiBenchmarkResponseClassification.ExpectedResponse, 200, null),
            new(20, 200, ApiBenchmarkResponseClassification.ExpectedResponse, 200, null),
            new(30, 300, ApiBenchmarkResponseClassification.ExpectedResponse, 200, null),
            new(40, 400, ApiBenchmarkResponseClassification.RateLimited, 429, 2),
            new(null, 0, ApiBenchmarkResponseClassification.TimeoutOrCancellation, null, null),
            new(50, 50, ApiBenchmarkResponseClassification.UnexpectedHttpError, 500, null),
            new(60, 0, ApiBenchmarkResponseClassification.TransportFailure, null, null)
        ];

        ApiLatencyWindowMetrics result = ApiLatencyBenchmarkMetrics.Calculate(
            samples,
            TimeSpan.FromSeconds(2),
            before,
            after);

        result.SampleCount.ShouldBe(7);
        result.SuccessfulSampleCount.ShouldBe(3);
        result.P50Ms.ShouldBe(20);
        result.P95Ms.ShouldBe(30);
        result.P99Ms.ShouldBe(30);
        result.AttemptedThroughputRequestsPerSecond.ShouldBe(3.5);
        result.CompletedThroughputRequestsPerSecond.ShouldBe(2.5);
        result.SuccessfulThroughputRequestsPerSecond.ShouldBe(1.5);
        result.CompletedResponsePayloadBytes.ShouldBe(1_050);
        result.AverageResponsePayloadBytesPerCompletedResponse.ShouldBe(210);
        result.UnexpectedHttpErrorCount.ShouldBe(1);
        result.TimeoutOrCancellationCount.ShouldBe(1);
        result.TransportFailureCount.ShouldBe(1);
        result.RateLimitedCount.ShouldBe(1);
        result.RetryAfter.ShouldBe(new ApiBenchmarkRetryAfterSummary(1, 2, 2, 2));
        result.ProcessDelta.ShouldBe(new ApiBenchmarkProcessDelta(25, 250, 500, 2, 1, 1, -2, 1, 3));
    }

    [Theory]
    [InlineData(200, 200, false, ApiBenchmarkResponseClassification.ExpectedResponse)]
    [InlineData(429, 200, false, ApiBenchmarkResponseClassification.RateLimited)]
    [InlineData(500, 200, false, ApiBenchmarkResponseClassification.UnexpectedHttpError)]
    [InlineData(null, 200, true, ApiBenchmarkResponseClassification.TimeoutOrCancellation)]
    [InlineData(null, 200, false, ApiBenchmarkResponseClassification.TransportFailure)]
    public void Classify_ShouldKeepRateLimitsTimeoutsAndTransportFailuresDistinct(
        int? statusCode,
        int expectedStatusCode,
        bool timedOutOrCancelled,
        ApiBenchmarkResponseClassification expected)
    {
        ApiLatencyBenchmarkMetrics.Classify(statusCode, expectedStatusCode, timedOutOrCancelled)
            .ShouldBe(expected);
    }

    [Fact]
    public void Percentile_ShouldUseNearestRankAndRejectInvalidPercentiles()
    {
        double[] ordered = [10, 20, 30, 40, 50];

        ApiLatencyBenchmarkMetrics.Percentile(ordered, .50).ShouldBe(30);
        ApiLatencyBenchmarkMetrics.Percentile(ordered, .95).ShouldBe(50);
        ApiLatencyBenchmarkMetrics.Percentile(ordered, .99).ShouldBe(50);
        ApiLatencyBenchmarkMetrics.Percentile([], .50).ShouldBeNull();
        Should.Throw<ArgumentOutOfRangeException>(() =>
            ApiLatencyBenchmarkMetrics.Percentile(ordered, 1.01));
    }

    [Fact]
    public void ParseRetryAfterSeconds_ShouldHandleDeltaFuturePastAndMissingValuesDeterministically()
    {
        DateTimeOffset nowUtc = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

        ApiLatencyBenchmarkMetrics.ParseRetryAfterSeconds(
            new RetryConditionHeaderValue(TimeSpan.FromSeconds(15)), nowUtc).ShouldBe(15);
        ApiLatencyBenchmarkMetrics.ParseRetryAfterSeconds(
            new RetryConditionHeaderValue(nowUtc.AddSeconds(30)), nowUtc).ShouldBe(30);
        ApiLatencyBenchmarkMetrics.ParseRetryAfterSeconds(
            new RetryConditionHeaderValue(nowUtc.AddSeconds(-1)), nowUtc).ShouldBe(0);
        ApiLatencyBenchmarkMetrics.ParseRetryAfterSeconds(null, nowUtc).ShouldBeNull();
    }

    [Fact]
    public void Calculate_ShouldSummarizeRetryAfterValuesFromRateLimitedResponses()
    {
        ApiBenchmarkProcessSnapshot snapshot = new(0, 0, 0, 0, 0, 0, null, null, null);
        ApiBenchmarkSample[] samples =
        [
            new(20, 20, ApiBenchmarkResponseClassification.RateLimited, 429, 5)
        ];

        ApiLatencyWindowMetrics result = ApiLatencyBenchmarkMetrics.Calculate(
            samples,
            TimeSpan.FromSeconds(1),
            snapshot,
            snapshot);

        result.RetryAfter.ShouldBe(new ApiBenchmarkRetryAfterSummary(1, 5, 5, 5));
    }

    [Theory]
    [InlineData(ApiBenchmarkResponseClassification.ExpectedResponse, null, 200)]
    [InlineData(ApiBenchmarkResponseClassification.ExpectedResponse, 10d, null)]
    [InlineData(ApiBenchmarkResponseClassification.TimeoutOrCancellation, 10d, 408)]
    [InlineData(ApiBenchmarkResponseClassification.TransportFailure, 10d, 503)]
    public void Calculate_ShouldRejectInvalidSampleClassificationInvariants(
        ApiBenchmarkResponseClassification classification,
        double? elapsedMs,
        int? statusCode)
    {
        ApiBenchmarkProcessSnapshot snapshot = new(0, 0, 0, 0, 0, 0, null, null, null);
        ApiBenchmarkSample[] samples = [new(elapsedMs, 0, classification, statusCode, null)];

        Should.Throw<ArgumentException>(() => ApiLatencyBenchmarkMetrics.Calculate(
            samples,
            TimeSpan.FromSeconds(1),
            snapshot,
            snapshot));
    }

    [Fact]
    public void Calculate_ShouldRejectNegativeSampleMeasurements()
    {
        ApiBenchmarkProcessSnapshot snapshot = new(0, 0, 0, 0, 0, 0, null, null, null);
        ApiBenchmarkSample[] samples =
        [
            new(-1, 0, ApiBenchmarkResponseClassification.ExpectedResponse, 200, null)
        ];

        Should.Throw<ArgumentOutOfRangeException>(() => ApiLatencyBenchmarkMetrics.Calculate(
            samples,
            TimeSpan.FromSeconds(1),
            snapshot,
            snapshot));
    }

    [Theory]
    [InlineData(ApiBenchmarkResponseClassification.TimeoutOrCancellation, 1, null)]
    [InlineData(ApiBenchmarkResponseClassification.TransportFailure, 0, 1d)]
    [InlineData(ApiBenchmarkResponseClassification.UnexpectedHttpError, 0, 1d)]
    public void Calculate_ShouldRejectResponseDataOnNonResponseOrNonRateLimitedSamples(
        ApiBenchmarkResponseClassification classification,
        int responsePayloadBytes,
        double? retryAfterSeconds)
    {
        ApiBenchmarkProcessSnapshot snapshot = new(0, 0, 0, 0, 0, 0, null, null, null);
        int? statusCode = classification == ApiBenchmarkResponseClassification.UnexpectedHttpError ? 503 : null;
        ApiBenchmarkSample[] samples =
        [
            new(10, responsePayloadBytes, classification, statusCode, retryAfterSeconds)
        ];

        Should.Throw<ArgumentException>(() => ApiLatencyBenchmarkMetrics.Calculate(
            samples,
            TimeSpan.FromSeconds(1),
            snapshot,
            snapshot));
    }
}

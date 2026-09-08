namespace IntegrationTests.Performance;

public sealed class SqlCommandCounterInterceptorTests
{
    [Fact]
    public void Snapshot_RecordsSuccessFailureCancellationAndApproximateDuration()
    {
        var collector = new SqlCommandCounterInterceptor();
        collector.RecordForTest(TimeSpan.FromMilliseconds(1), SqlCommandCompletionKind.Succeeded);
        collector.RecordForTest(TimeSpan.FromMilliseconds(8), SqlCommandCompletionKind.Failed);
        collector.RecordForTest(TimeSpan.FromMilliseconds(16), SqlCommandCompletionKind.Cancelled);

        SqlCommandDurationSnapshot snapshot = collector.Snapshot();

        snapshot.Count.ShouldBe(3);
        snapshot.Succeeded.ShouldBe(1);
        snapshot.Failed.ShouldBe(1);
        snapshot.Cancelled.ShouldBe(1);
        snapshot.TotalDurationMs.ShouldBe(25d);
        snapshot.ApproximateP50Ms.ShouldNotBeNull();
        snapshot.ApproximateP95Ms.ShouldNotBeNull();
        snapshot.MaxDurationMs.ShouldBe(16d);
    }

    [Fact]
    public async Task ResetAndConcurrentRecording_DoNotMixPriorCompletedWindow()
    {
        var collector = new SqlCommandCounterInterceptor();
        await Task.WhenAll(Enumerable.Range(0, 512).Select(_ => Task.Run(() =>
            collector.RecordForTest(TimeSpan.FromMilliseconds(1), SqlCommandCompletionKind.Succeeded))));
        collector.Snapshot().Count.ShouldBe(512);

        collector.Reset();
        collector.Snapshot().Count.ShouldBe(0);
        collector.Snapshot().ApproximateP95Ms.ShouldBeNull();
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(10_000L)] // 1ms
    [InlineData(1_024L)] // exact power-of-two boundary
    [InlineData(1_025L)] // immediately above the boundary
    [InlineData(long.MaxValue)]
    public void ApproximatePercentile_IsBoundedUpperBoundForEveryBucketBoundary(long ticks)
    {
        var collector = new SqlCommandCounterInterceptor();
        var duration = TimeSpan.FromTicks(ticks);
        collector.RecordForTest(duration, SqlCommandCompletionKind.Succeeded);

        SqlCommandDurationSnapshot snapshot = collector.Snapshot();

        snapshot.ApproximateP50Ms.ShouldNotBeNull();
        snapshot.ApproximateP50Ms!.Value.ShouldBeGreaterThanOrEqualTo(duration.TotalMilliseconds);
        snapshot.ApproximateP95Ms!.Value.ShouldBeGreaterThanOrEqualTo(duration.TotalMilliseconds);
        snapshot.ApproximateP99Ms!.Value.ShouldBeGreaterThanOrEqualTo(duration.TotalMilliseconds);
        snapshot.ApproximateP99Ms!.Value.ShouldBeLessThanOrEqualTo(TimeSpan.MaxValue.TotalMilliseconds);
    }
}

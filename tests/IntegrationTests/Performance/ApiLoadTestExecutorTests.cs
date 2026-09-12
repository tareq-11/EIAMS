namespace IntegrationTests.Performance;

public sealed class ApiLoadTestExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_AsyncPhaseHooks_ShouldRunInOrderAndStopEachPhaseMonitor()
    {
        var clock = new DeterministicClock();
        var events = new List<string>();
        var executor = new ApiLoadTestExecutor(clock, (_, _) =>
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            return Task.FromResult(new ApiLoadTestExecutionSample(Succeeded: true));
        });

        await executor.ExecuteAsync(
            CreateClosedLoopRun(1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)),
            (phase, _) => { events.Add($"start:{phase}"); return Task.CompletedTask; },
            (phase, _) => { events.Add($"stop:{phase}"); return Task.CompletedTask; });

        events.ShouldBe([
            "start:Warmup", "stop:Warmup", "start:Measurement", "stop:Measurement"]);
    }

    [Fact]
    public async Task ExecuteAsync_AsyncPhaseHooks_ShouldStopWarmupAndSkipMeasurement_WhenCancelled()
    {
        var clock = new DeterministicClock();
        var events = new List<string>();
        var executor = new ApiLoadTestExecutor(clock, (_, _) =>
            Task.FromResult(new ApiLoadTestExecutionSample(Succeeded: true)));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await executor.ExecuteAsync(
            CreateClosedLoopRun(1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)),
            (phase, _) => { events.Add($"start:{phase}"); return Task.CompletedTask; },
            (phase, _) => { events.Add($"stop:{phase}"); return Task.CompletedTask; },
            cancellation.Token);

        events.ShouldBe(["start:Warmup", "stop:Warmup"]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldKeepWarmupSamplesOutOfTheMeasurementSummary()
    {
        var clock = new DeterministicClock();
        var executor = new ApiLoadTestExecutor(clock, (_, _) =>
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            return Task.FromResult(new ApiLoadTestExecutionSample(Succeeded: true));
        });

        ApiLoadTestExecutionResult result = await executor.ExecuteAsync(CreateClosedLoopRun(
            concurrency: 1,
            warmup: TimeSpan.FromSeconds(2),
            measurement: TimeSpan.FromSeconds(3)));

        result.Warmup.StartedCount.ShouldBe(2);
        result.Measurement.StartedCount.ShouldBe(3);
        result.Warmup.CompletedCount.ShouldBe(2);
        result.Measurement.CompletedCount.ShouldBe(3);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldBoundConstantArrivalInFlightWorkAndRecordDrops()
    {
        var clock = new DeterministicClock();
        var completion = new TaskCompletionSource<ApiLoadTestExecutionSample>();
        var executor = new ApiLoadTestExecutor(clock, (_, _) => completion.Task);
        ApiLoadTestRunDefinition run = CreateConstantArrivalRun(
            concurrency: 1,
            arrivalRate: 10,
            warmup: TimeSpan.Zero,
            measurement: TimeSpan.FromSeconds(1));

        ApiLoadTestExecutionResult result = await executor.ExecuteAsync(run);
        completion.SetResult(new ApiLoadTestExecutionSample(Succeeded: true));

        result.Measurement.StartedCount.ShouldBe(1);
        result.Measurement.DroppedSaturationCount.ShouldBe(9);
        result.Measurement.MaximumInFlightCount.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNeverExceedClosedLoopWorkerConcurrency()
    {
        var clock = new DeterministicClock();
        var completion = new TaskCompletionSource<ApiLoadTestExecutionSample>();
        var executor = new ApiLoadTestExecutor(
            clock,
            (_, _) => completion.Task,
            new ApiLoadTestExecutorOptions(TimeSpan.FromSeconds(1)));

        ApiLoadTestExecutionResult result = await executor.ExecuteAsync(CreateClosedLoopRun(
            concurrency: 2,
            warmup: TimeSpan.Zero,
            measurement: TimeSpan.FromSeconds(1)));
        completion.SetResult(new ApiLoadTestExecutionSample(Succeeded: true));

        result.Measurement.StartedCount.ShouldBe(2);
        result.Measurement.MaximumInFlightCount.ShouldBe(2);
        result.Measurement.MaximumInFlightCount.ShouldBeLessThanOrEqualTo(2);
        result.ShutdownGracePeriodElapsed.ShouldBeTrue();
        result.Measurement.InFlightAtSummaryCount.ShouldBe(2);
        result.Measurement.UnfinishedAfterGraceCount.ShouldBe(2);
        clock.Elapsed.ShouldBe(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRunClosedLoopUntilDeadlineBeforeStartingGracePeriod()
    {
        var clock = new DeterministicClock();
        var executor = new ApiLoadTestExecutor(
            clock,
            (_, _) =>
            {
                clock.Advance(TimeSpan.FromSeconds(1));
                return Task.FromResult(new ApiLoadTestExecutionSample(Succeeded: true));
            },
            new ApiLoadTestExecutorOptions(TimeSpan.FromSeconds(1)));

        ApiLoadTestExecutionResult result = await executor.ExecuteAsync(CreateClosedLoopRun(
            concurrency: 1,
            warmup: TimeSpan.Zero,
            measurement: TimeSpan.FromSeconds(5)));

        result.Measurement.StartedCount.ShouldBe(5);
        result.ShutdownGracePeriodElapsed.ShouldBeFalse();
        clock.Elapsed.ShouldBe(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountFaultedRequestsWithoutLeakingFaultedTasks()
    {
        var clock = new DeterministicClock();
        var executor = new ApiLoadTestExecutor(clock, (_, _) =>
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            return Task.FromException<ApiLoadTestExecutionSample>(new InvalidOperationException());
        });

        ApiLoadTestExecutionResult result = await executor.ExecuteAsync(CreateClosedLoopRun(
            concurrency: 1,
            warmup: TimeSpan.Zero,
            measurement: TimeSpan.FromSeconds(3)));

        result.Measurement.StartedCount.ShouldBe(3);
        result.Measurement.CompletedCount.ShouldBe(0);
        result.Measurement.FaultedCount.ShouldBe(3);
        result.Measurement.InFlightAtSummaryCount.ShouldBe(0);
        result.Measurement.UnfinishedAfterGraceCount.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotStartMeasurementWhenWarmupExceedsGracePeriod()
    {
        var clock = new DeterministicClock();
        var completion = new TaskCompletionSource<ApiLoadTestExecutionSample>();
        var executor = new ApiLoadTestExecutor(
            clock,
            (_, _) => completion.Task,
            new ApiLoadTestExecutorOptions(TimeSpan.FromSeconds(1)));

        ApiLoadTestExecutionResult result = await executor.ExecuteAsync(CreateClosedLoopRun(
            concurrency: 1,
            warmup: TimeSpan.FromSeconds(1),
            measurement: TimeSpan.FromSeconds(1)));
        completion.SetResult(new ApiLoadTestExecutionSample(Succeeded: true));

        result.ShutdownGracePeriodElapsed.ShouldBeTrue();
        result.Warmup.UnfinishedAfterGraceCount.ShouldBe(1);
        result.Measurement.StartedCount.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStopCleanlyWhenCancelledBeforeExecution()
    {
        var clock = new DeterministicClock();
        var executor = new ApiLoadTestExecutor(clock, (_, _) =>
            Task.FromResult(new ApiLoadTestExecutionSample(Succeeded: true)));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        ApiLoadTestExecutionResult result = await executor.ExecuteAsync(CreateClosedLoopRun(
            concurrency: 1,
            warmup: TimeSpan.FromSeconds(1),
            measurement: TimeSpan.FromSeconds(1)), cancellation.Token);

        result.WasCancelled.ShouldBeTrue();
        result.Warmup.StartedCount.ShouldBe(0);
        result.Measurement.StartedCount.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_PhaseHooksFollowPhaseOrderAndDoNotClaimCancelledMeasurement()
    {
        var clock = new DeterministicClock();
        var events = new List<string>();
        var executor = new ApiLoadTestExecutor(clock, (_, _) =>
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            return Task.FromResult(new ApiLoadTestExecutionSample(Succeeded: true));
        });

        await executor.ExecuteAsync(
            CreateClosedLoopRun(1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)),
            phase => events.Add($"before:{phase}"),
            phase => events.Add($"after:{phase}"));

        events.ShouldBe([
            "before:Warmup", "after:Warmup", "before:Measurement", "after:Measurement"
        ]);

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        events.Clear();
        await executor.ExecuteAsync(
            CreateClosedLoopRun(1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)),
            phase => events.Add($"before:{phase}"),
            phase => events.Add($"after:{phase}"),
            cancellation.Token);

        events.ShouldBe(["before:Warmup", "after:Warmup"]);
    }

    [Fact]
    public async Task ExecuteAsync_PhaseHooksCanResetSqlCollectorWithoutMixingPhaseSnapshots()
    {
        var clock = new DeterministicClock();
        var collector = new SqlCommandCounterInterceptor();
        var snapshots = new List<SqlCommandDurationSnapshot>();
        var executor = new ApiLoadTestExecutor(clock, (_, _) =>
        {
            collector.RecordForTest(TimeSpan.FromMilliseconds(1), SqlCommandCompletionKind.Succeeded);
            clock.Advance(TimeSpan.FromSeconds(1));
            return Task.FromResult(new ApiLoadTestExecutionSample(Succeeded: true));
        });

        await executor.ExecuteAsync(
            CreateClosedLoopRun(1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)),
            _ => collector.Reset(),
            _ => snapshots.Add(collector.Snapshot()));

        snapshots.Count.ShouldBe(2);
        snapshots[0].Count.ShouldBe(1);
        snapshots[1].Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSelectTheSameMeasurementScenariosForTheSameRunSeed()
    {
        ApiLoadTestRunDefinition run = CreateClosedLoopRun(
            concurrency: 1,
            warmup: TimeSpan.Zero,
            measurement: TimeSpan.FromSeconds(4));
        var first = new List<ApiLoadTestScenario>();
        var second = new List<ApiLoadTestScenario>();

        await CreateRecordingExecutor(first).ExecuteAsync(run);
        await CreateRecordingExecutor(second).ExecuteAsync(run);

        first.ShouldBe(second);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAggregateWithoutRetainingSamplesForLargeSimulatedRuns()
    {
        var clock = new DeterministicClock();
        var executor = new ApiLoadTestExecutor(clock, (_, _) =>
            Task.FromResult(new ApiLoadTestExecutionSample(Succeeded: true)));
        ApiLoadTestRunDefinition run = CreateConstantArrivalRun(
            concurrency: 50,
            arrivalRate: 10_000,
            warmup: TimeSpan.Zero,
            measurement: TimeSpan.FromSeconds(10));

        ApiLoadTestExecutionResult result = await executor.ExecuteAsync(run);

        result.Measurement.StartedCount.ShouldBe(100_000);
        result.Measurement.CompletedCount.ShouldBe(100_000);
        result.Measurement.RetainedSampleCount.ShouldBe(0);
        result.Measurement.MaximumInFlightCount.ShouldBeLessThanOrEqualTo(50);
    }

    private static ApiLoadTestExecutor CreateRecordingExecutor(List<ApiLoadTestScenario> scenarios)
    {
        var clock = new DeterministicClock();
        return new ApiLoadTestExecutor(clock, (scenario, _) =>
        {
            scenarios.Add(scenario);
            clock.Advance(TimeSpan.FromSeconds(1));
            return Task.FromResult(new ApiLoadTestExecutionSample(Succeeded: true));
        });
    }

    private static ApiLoadTestRunDefinition CreateClosedLoopRun(
        int concurrency,
        TimeSpan warmup,
        TimeSpan measurement) => new(
        runNumber: 1,
        clientConcurrency: concurrency,
        mode: ApiLoadTestMode.ClosedLoop,
        arrivalRatePerSecond: null,
        seed: 42,
        warmup: new ApiLoadTestPhase(ApiLoadTestPhaseKind.Warmup, warmup),
        measurement: new ApiLoadTestPhase(ApiLoadTestPhaseKind.Measurement, measurement));

    private static ApiLoadTestRunDefinition CreateConstantArrivalRun(
        int concurrency,
        double arrivalRate,
        TimeSpan warmup,
        TimeSpan measurement) => new(
        runNumber: 1,
        clientConcurrency: concurrency,
        mode: ApiLoadTestMode.ConstantArrivalRate,
        arrivalRatePerSecond: arrivalRate,
        seed: 42,
        warmup: new ApiLoadTestPhase(ApiLoadTestPhaseKind.Warmup, warmup),
        measurement: new ApiLoadTestPhase(ApiLoadTestPhaseKind.Measurement, measurement));

    private sealed class DeterministicClock : IApiLoadTestClock
    {
        public TimeSpan Elapsed { get; private set; }

        public Task DelayUntilAsync(TimeSpan deadline, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Elapsed = TimeSpan.FromTicks(Math.Max(Elapsed.Ticks, deadline.Ticks));
            return Task.CompletedTask;
        }

        internal void Advance(TimeSpan duration) => Elapsed += duration;
    }
}

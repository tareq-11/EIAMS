using System.Diagnostics;

namespace IntegrationTests.Performance;

internal sealed record ApiLoadTestExecutionSample(bool Succeeded);

internal delegate Task<ApiLoadTestExecutionSample> ApiLoadTestScenarioExecutor(
    ApiLoadTestScenario scenario,
    CancellationToken cancellationToken);

internal interface IApiLoadTestClock
{
    TimeSpan Elapsed { get; }

    Task DelayUntilAsync(TimeSpan deadline, CancellationToken cancellationToken);
}

internal sealed record ApiLoadTestExecutorOptions(TimeSpan ShutdownGracePeriod)
{
    internal static readonly ApiLoadTestExecutorOptions Default = new(TimeSpan.FromSeconds(10));
}

internal sealed record ApiLoadTestPhaseExecutionSummary(
    int StartedCount,
    int CompletedCount,
    int SuccessfulCount,
    int CancelledCount,
    int FaultedCount,
    int DroppedSaturationCount,
    int MaximumInFlightCount,
    int InFlightAtSummaryCount,
    int UnfinishedAfterGraceCount,
    bool CancellationRequested,
    int RetainedSampleCount);

internal sealed record ApiLoadTestExecutionResult(
    ApiLoadTestPhaseExecutionSummary Warmup,
    ApiLoadTestPhaseExecutionSummary Measurement,
    bool WasCancelled,
    bool ShutdownGracePeriodElapsed);

/// <summary>
/// Runs a bounded mixed-workload definition. It aggregates samples online and never retains per-request results.
/// </summary>
internal sealed class ApiLoadTestExecutor(
    IApiLoadTestClock clock,
    ApiLoadTestScenarioExecutor executeScenario,
    ApiLoadTestExecutorOptions? options = null)
{
    private readonly IApiLoadTestClock clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private readonly ApiLoadTestScenarioExecutor executeScenario = executeScenario ??
        throw new ArgumentNullException(nameof(executeScenario));
    private readonly ApiLoadTestExecutorOptions options = options ?? ApiLoadTestExecutorOptions.Default;

    internal Task<ApiLoadTestExecutionResult> ExecuteAsync(
        ApiLoadTestRunDefinition run,
        CancellationToken cancellationToken) =>
        ExecuteAsync(run, beforePhase: null, afterPhase: null, cancellationToken);

    internal async Task<ApiLoadTestExecutionResult> ExecuteAsync(
        ApiLoadTestRunDefinition run,
        Action<ApiLoadTestPhaseKind>? beforePhase = null,
        Action<ApiLoadTestPhaseKind>? afterPhase = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ValidateOptions(options);

        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        beforePhase?.Invoke(ApiLoadTestPhaseKind.Warmup);
        PhaseAccumulator warmup = await ExecutePhaseAsync(
            run,
            run.Warmup,
            executionCancellation,
            cancellationToken).ConfigureAwait(false);
        afterPhase?.Invoke(ApiLoadTestPhaseKind.Warmup);
        PhaseAccumulator measurement;
        if (executionCancellation.IsCancellationRequested)
        {
            measurement = new PhaseAccumulator();
        }
        else
        {
            beforePhase?.Invoke(ApiLoadTestPhaseKind.Measurement);
            measurement = await ExecutePhaseAsync(
                run,
                run.Measurement,
                executionCancellation,
                cancellationToken).ConfigureAwait(false);
            afterPhase?.Invoke(ApiLoadTestPhaseKind.Measurement);
        }

        return new ApiLoadTestExecutionResult(
            warmup.ToSummary(),
            measurement.ToSummary(),
            cancellationToken.IsCancellationRequested,
            warmup.ShutdownGracePeriodElapsed || measurement.ShutdownGracePeriodElapsed);
    }

    /// <summary>Async phase hooks for monitors which must be started and stopped without blocking threads.</summary>
    internal async Task<ApiLoadTestExecutionResult> ExecuteAsync(
        ApiLoadTestRunDefinition run,
        Func<ApiLoadTestPhaseKind, CancellationToken, Task>? beforePhaseAsync,
        Func<ApiLoadTestPhaseKind, CancellationToken, Task>? afterPhaseAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ValidateOptions(options);
        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        PhaseAccumulator warmup = await ExecuteWithHooksAsync(run.Warmup, ApiLoadTestPhaseKind.Warmup).ConfigureAwait(false);
        PhaseAccumulator measurement = executionCancellation.IsCancellationRequested
            ? new PhaseAccumulator()
            : await ExecuteWithHooksAsync(run.Measurement, ApiLoadTestPhaseKind.Measurement).ConfigureAwait(false);
        return new ApiLoadTestExecutionResult(warmup.ToSummary(), measurement.ToSummary(), cancellationToken.IsCancellationRequested,
            warmup.ShutdownGracePeriodElapsed || measurement.ShutdownGracePeriodElapsed);

        async Task<PhaseAccumulator> ExecuteWithHooksAsync(ApiLoadTestPhase phase, ApiLoadTestPhaseKind kind)
        {
            if (beforePhaseAsync is not null)
            {
                await beforePhaseAsync(kind, cancellationToken).ConfigureAwait(false);
            }
            try
            {
                return await ExecutePhaseAsync(run, phase, executionCancellation, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (afterPhaseAsync is not null)
                {
                    await afterPhaseAsync(kind, CancellationToken.None).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task<PhaseAccumulator> ExecutePhaseAsync(
        ApiLoadTestRunDefinition run,
        ApiLoadTestPhase phase,
        CancellationTokenSource executionCancellation,
        CancellationToken externalCancellationToken)
    {
        var accumulator = new PhaseAccumulator();
        TimeSpan phaseDeadline = clock.Elapsed + phase.Duration;
        return run.Mode switch
        {
            ApiLoadTestMode.ClosedLoop => await ExecuteClosedLoopAsync(
                run,
                phaseDeadline,
                accumulator,
                executionCancellation,
                externalCancellationToken).ConfigureAwait(false),
            ApiLoadTestMode.ConstantArrivalRate => await ExecuteConstantArrivalRateAsync(
                run,
                phaseDeadline,
                accumulator,
                executionCancellation,
                externalCancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(run), run.Mode, "Unknown load-test mode.")
        };
    }

    private async Task<PhaseAccumulator> ExecuteClosedLoopAsync(
        ApiLoadTestRunDefinition run,
        TimeSpan phaseDeadline,
        PhaseAccumulator accumulator,
        CancellationTokenSource executionCancellation,
        CancellationToken externalCancellationToken)
    {
        Task[] workers = Enumerable.Range(0, run.ClientConcurrency)
            .Select(worker => ExecuteClosedLoopWorkerAsync(
                run,
                worker,
                phaseDeadline,
                accumulator,
                executionCancellation.Token,
                externalCancellationToken))
            .ToArray();
        await WaitForPhaseDeadlineAsync(
            phaseDeadline,
            accumulator,
            executionCancellation.Token).ConfigureAwait(false);
        await AwaitBoundedAsync(workers, accumulator, executionCancellation).ConfigureAwait(false);
        return accumulator;
    }

    private async Task ExecuteClosedLoopWorkerAsync(
        ApiLoadTestRunDefinition run,
        int workerNumber,
        TimeSpan phaseDeadline,
        PhaseAccumulator accumulator,
        CancellationToken executionToken,
        CancellationToken externalCancellationToken)
    {
        ulong requestIndex = (ulong)workerNumber;
        while (!externalCancellationToken.IsCancellationRequested && clock.Elapsed < phaseDeadline)
        {
            accumulator.Start();
            try
            {
                ApiLoadTestExecutionSample sample = await executeScenario(
                    run.SelectScenario(requestIndex),
                    executionToken).ConfigureAwait(false);
                accumulator.Complete(sample);
            }
            catch (OperationCanceledException) when (executionToken.IsCancellationRequested)
            {
                accumulator.Cancel();
                return;
            }
            catch (Exception exception) when (!IsFatal(exception))
            {
                accumulator.Fault();
            }
            finally
            {
                accumulator.EndInFlight();
            }

            requestIndex += (ulong)run.ClientConcurrency;
        }
    }

    private async Task<PhaseAccumulator> ExecuteConstantArrivalRateAsync(
        ApiLoadTestRunDefinition run,
        TimeSpan phaseDeadline,
        PhaseAccumulator accumulator,
        CancellationTokenSource executionCancellation,
        CancellationToken externalCancellationToken)
    {
        double arrivalRate = run.ArrivalRatePerSecond ?? throw new InvalidOperationException(
            "A constant-arrival-rate run requires an arrival rate.");
        TimeSpan phaseStart = clock.Elapsed;
        ulong requestIndex = 0;
        var inFlight = new List<Task>(run.ClientConcurrency);

        while (!externalCancellationToken.IsCancellationRequested)
        {
            TimeSpan scheduledAt = phaseStart + TimeSpan.FromSeconds(requestIndex / arrivalRate);
            if (scheduledAt >= phaseDeadline)
            {
                break;
            }

            try
            {
                await clock.DelayUntilAsync(scheduledAt, executionCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (executionCancellation.IsCancellationRequested)
            {
                break;
            }

            await RemoveCompletedAsync(inFlight).ConfigureAwait(false);
            if (clock.Elapsed >= phaseDeadline || externalCancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (inFlight.Count >= run.ClientConcurrency)
            {
                accumulator.DropForSaturation();
            }
            else
            {
                accumulator.Start();
                inFlight.Add(ExecuteInFlightAsync(
                    run.SelectScenario(requestIndex),
                    accumulator,
                    executionCancellation.Token));
            }

            requestIndex++;
        }

        await AwaitBoundedAsync(inFlight, accumulator, executionCancellation).ConfigureAwait(false);
        return accumulator;
    }

    private async Task ExecuteInFlightAsync(
        ApiLoadTestScenario scenario,
        PhaseAccumulator accumulator,
        CancellationToken executionToken)
    {
        try
        {
            ApiLoadTestExecutionSample sample = await executeScenario(scenario, executionToken).ConfigureAwait(false);
            accumulator.Complete(sample);
        }
        catch (OperationCanceledException) when (executionToken.IsCancellationRequested)
        {
            accumulator.Cancel();
        }
        catch (Exception exception) when (!IsFatal(exception))
        {
            accumulator.Fault();
        }
        finally
        {
            accumulator.EndInFlight();
        }
    }

    private async Task AwaitBoundedAsync(
        IReadOnlyCollection<Task> tasks,
        PhaseAccumulator accumulator,
        CancellationTokenSource executionCancellation)
    {
        if (tasks.Count == 0)
        {
            return;
        }

        var all = Task.WhenAll(tasks);
        if (all.IsCompleted)
        {
            await all.ConfigureAwait(false);
            return;
        }

        Task grace = clock.DelayUntilAsync(clock.Elapsed + options.ShutdownGracePeriod, CancellationToken.None);
        if (await Task.WhenAny(all, grace).ConfigureAwait(false) != all)
        {
            accumulator.MarkShutdownGracePeriodElapsed();
            await executionCancellation.CancelAsync().ConfigureAwait(false);
        }
        else
        {
            await all.ConfigureAwait(false);
        }
    }

    private async Task WaitForPhaseDeadlineAsync(
        TimeSpan phaseDeadline,
        PhaseAccumulator accumulator,
        CancellationToken executionToken)
    {
        try
        {
            await clock.DelayUntilAsync(phaseDeadline, executionToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (executionToken.IsCancellationRequested)
        {
            accumulator.MarkCancellationRequested();
        }
    }

    private static async Task RemoveCompletedAsync(List<Task> inFlight)
    {
        for (int index = inFlight.Count - 1; index >= 0; index--)
        {
            if (inFlight[index].IsCompleted)
            {
                await inFlight[index].ConfigureAwait(false);
                inFlight.RemoveAt(index);
            }
        }
    }

    private static void ValidateOptions(ApiLoadTestExecutorOptions options)
    {
        if (options.ShutdownGracePeriod < TimeSpan.Zero ||
            options.ShutdownGracePeriod > TimeSpan.FromMinutes(1))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Shutdown grace period must be between zero and one minute.");
        }
    }

    private static bool IsFatal(Exception exception) => exception is
        OutOfMemoryException or
        StackOverflowException or
        AccessViolationException or
        AppDomainUnloadedException or
        BadImageFormatException or
        CannotUnloadAppDomainException;

    private sealed class PhaseAccumulator
    {
        private int startedCount;
        private int completedCount;
        private int successfulCount;
        private int cancelledCount;
        private int faultedCount;
        private int droppedSaturationCount;
        private int inFlightCount;
        private int maximumInFlightCount;

        internal bool ShutdownGracePeriodElapsed { get; private set; }
        internal bool CancellationRequested { get; private set; }

        internal void Start()
        {
            Interlocked.Increment(ref startedCount);
            int currentInFlight = Interlocked.Increment(ref inFlightCount);
            while (true)
            {
                int observedMaximum = Volatile.Read(ref maximumInFlightCount);
                if (currentInFlight <= observedMaximum ||
                    Interlocked.CompareExchange(ref maximumInFlightCount, currentInFlight, observedMaximum) == observedMaximum)
                {
                    return;
                }
            }
        }

        internal void Complete(ApiLoadTestExecutionSample sample)
        {
            Interlocked.Increment(ref completedCount);
            if (sample.Succeeded)
            {
                Interlocked.Increment(ref successfulCount);
            }
        }

        internal void DropForSaturation() => Interlocked.Increment(ref droppedSaturationCount);

        internal void Cancel() => Interlocked.Increment(ref cancelledCount);

        internal void Fault() => Interlocked.Increment(ref faultedCount);

        internal void EndInFlight() => Interlocked.Decrement(ref inFlightCount);

        internal void MarkShutdownGracePeriodElapsed() => ShutdownGracePeriodElapsed = true;

        internal void MarkCancellationRequested() => CancellationRequested = true;

        internal ApiLoadTestPhaseExecutionSummary ToSummary() => new(
            Volatile.Read(ref startedCount),
            Volatile.Read(ref completedCount),
            Volatile.Read(ref successfulCount),
            Volatile.Read(ref cancelledCount),
            Volatile.Read(ref faultedCount),
            Volatile.Read(ref droppedSaturationCount),
            Volatile.Read(ref maximumInFlightCount),
            Volatile.Read(ref inFlightCount),
            ShutdownGracePeriodElapsed ? Volatile.Read(ref inFlightCount) : 0,
            CancellationRequested,
            RetainedSampleCount: 0);
    }
}

internal sealed class StopwatchApiLoadTestClock : IApiLoadTestClock
{
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();

    public TimeSpan Elapsed => stopwatch.Elapsed;

    public Task DelayUntilAsync(TimeSpan deadline, CancellationToken cancellationToken)
    {
        TimeSpan delay = deadline - Elapsed;
        return delay > TimeSpan.Zero ? Task.Delay(delay, cancellationToken) : Task.CompletedTask;
    }
}

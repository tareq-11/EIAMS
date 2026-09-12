namespace IntegrationTests.Performance;

/// <summary>
/// Integrates sampled occupancy with a left-endpoint assumption: an observed waiting-session
/// count applies until the next observation (or stop). It is an estimate, not exact duration.
/// </summary>
internal sealed class SampledOccupancyIntegrator
{
    private long waitingSessionTicks;
    private long lastObservedAtTicks;
    private int lastWaitingSessions;
    private int hasObservation;
    private int completed;

    internal double ObservedWaitingSessionMilliseconds =>
        Interlocked.Read(ref waitingSessionTicks) / (double)TimeSpan.TicksPerMillisecond;

    internal void Record(int waitingSessions, long observedAtTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(waitingSessions);
        if (Volatile.Read(ref hasObservation) != 0)
        {
            AddInterval(lastWaitingSessions, observedAtTicks - lastObservedAtTicks);
        }
        lastWaitingSessions = waitingSessions;
        lastObservedAtTicks = observedAtTicks;
        Volatile.Write(ref hasObservation, 1);
    }

    internal void Complete(long completedAtTicks)
    {
        if (Interlocked.Exchange(ref completed, 1) != 0)
        {
            return;
        }
        if (Volatile.Read(ref hasObservation) != 0)
        {
            AddInterval(lastWaitingSessions, completedAtTicks - lastObservedAtTicks);
            lastObservedAtTicks = completedAtTicks;
        }
    }

    internal void Reset()
    {
        Interlocked.Exchange(ref waitingSessionTicks, 0);
        lastObservedAtTicks = 0;
        lastWaitingSessions = 0;
        Volatile.Write(ref hasObservation, 0);
        Volatile.Write(ref completed, 0);
    }

    private void AddInterval(int waitingSessions, long elapsedTicks)
    {
        if (waitingSessions <= 0 || elapsedTicks <= 0)
        {
            return;
        }
        long add = elapsedTicks > long.MaxValue / waitingSessions ? long.MaxValue : elapsedTicks * waitingSessions;
        while (true)
        {
            long current = Interlocked.Read(ref waitingSessionTicks);
            long next = current > long.MaxValue - add ? long.MaxValue : current + add;
            if (Interlocked.CompareExchange(ref waitingSessionTicks, next, current) == current)
            {
                return;
            }
        }
    }
}

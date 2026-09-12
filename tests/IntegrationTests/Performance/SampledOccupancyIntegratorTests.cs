namespace IntegrationTests.Performance;

public sealed class SampledOccupancyIntegratorTests
{
    [Fact]
    public void Record_ShouldIntegrateActualObservationCadence_NotNominalSamplingInterval()
    {
        var integrator = new SampledOccupancyIntegrator();

        integrator.Record(waitingSessions: 2, observedAtTicks: 100 * TimeSpan.TicksPerMillisecond);
        integrator.Record(waitingSessions: 1, observedAtTicks: 450 * TimeSpan.TicksPerMillisecond);
        integrator.Complete(700 * TimeSpan.TicksPerMillisecond);

        // Left endpoint: 2 sessions for the actual 350ms cadence, then 1 for 250ms.
        integrator.ObservedWaitingSessionMilliseconds.ShouldBe(950d);
    }

    [Fact]
    public void Record_ShouldSaturateInsteadOfOverflowing()
    {
        var integrator = new SampledOccupancyIntegrator();

        integrator.Record(int.MaxValue, 0);
        integrator.Complete(long.MaxValue);

        integrator.ObservedWaitingSessionMilliseconds.ShouldBe(long.MaxValue / (double)TimeSpan.TicksPerMillisecond);
    }

    [Fact]
    public void Complete_ShouldNotExtendStaleOccupancyAfterSamplerTermination()
    {
        var integrator = new SampledOccupancyIntegrator();

        integrator.Record(waitingSessions: 3, observedAtTicks: 100 * TimeSpan.TicksPerMillisecond);
        // This represents the sampler failing at 180ms, before a much later StopAsync call.
        integrator.Complete(180 * TimeSpan.TicksPerMillisecond);

        integrator.ObservedWaitingSessionMilliseconds.ShouldBe(240d);
        // A second completion from a later lifecycle caller does not extend the already-ended sample.
        integrator.Complete(900 * TimeSpan.TicksPerMillisecond);
        integrator.ObservedWaitingSessionMilliseconds.ShouldBe(240d);
    }
}

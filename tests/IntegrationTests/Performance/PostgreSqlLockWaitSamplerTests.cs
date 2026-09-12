using Npgsql;

namespace IntegrationTests.Performance;

[Collection(nameof(IntegrationTestCollection))]
public sealed class PostgreSqlLockWaitSamplerTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task Sampler_ShouldObserveActualAdvisoryLockContention_WithoutEmittingSessionDetails()
    {
        await using var sampler = new PostgreSqlLockWaitSampler(factory.DatabaseConnectionString, TimeSpan.FromMilliseconds(10));
        string resourceKey = $"sampler-test-{Guid.NewGuid():N}";
        await using IntegrationTestWebAppFactory.PostgresAdvisoryLockLease holder =
            await factory.HoldApplicationLockAsync(resourceKey);

        await sampler.StartAsync();
        Task waiter = WaitForSameAdvisoryLockAsync(factory.DatabaseConnectionString, resourceKey);
        await holder.WaitUntilContendedAsync();
        await WaitUntilAsync(() => sampler.Snapshot().SamplesWithLockWaits > 0);
        PostgreSqlLockWaitSnapshot snapshot = await sampler.StopAsync();

        snapshot.IsAvailable.ShouldBeTrue();
        snapshot.FailureCount.ShouldBe(0);
        snapshot.SampleCount.ShouldBeGreaterThan(0);
        snapshot.SamplesWithLockWaits.ShouldBeGreaterThan(0);
        snapshot.TotalWaitingSessionObservations.ShouldBeGreaterThan(0);
        snapshot.MaxConcurrentWaitingSessions.ShouldBeGreaterThan(0);
        snapshot.ApproximateObservedWaitingSessionMilliseconds.ShouldBeGreaterThan(0);

        await holder.ReleaseAsync();
        await waiter;
    }

    [Fact]
    public async Task Sampler_ShouldResetCountersForEachStartedWindow_WhenThereIsNoContention()
    {
        await using var sampler = new PostgreSqlLockWaitSampler(factory.DatabaseConnectionString, TimeSpan.FromMilliseconds(10));
        await sampler.StartAsync();
        await Task.Delay(35);
        PostgreSqlLockWaitSnapshot first = await sampler.StopAsync();
        await sampler.StartAsync();
        await Task.Delay(15);
        PostgreSqlLockWaitSnapshot second = await sampler.StopAsync();

        first.SampleCount.ShouldBeGreaterThan(0);
        first.SamplesWithLockWaits.ShouldBe(0);
        second.SampleCount.ShouldBeGreaterThan(0);
        second.SamplesWithLockWaits.ShouldBe(0);
        second.TotalWaitingSessionObservations.ShouldBe(0);
    }

    [Fact]
    public async Task Sampler_ShouldReportUnavailableAndFailure_WhenMonitoringConnectionCannotOpen()
    {
        await using var sampler = new PostgreSqlLockWaitSampler(
            "Host=127.0.0.1;Port=1;Database=unavailable;Username=unavailable;Timeout=1;Command Timeout=1",
            TimeSpan.FromMilliseconds(10));
        await sampler.StartAsync();
        await WaitUntilAsync(() => sampler.Snapshot().FailureCount > 0);
        PostgreSqlLockWaitSnapshot snapshot = await sampler.StopAsync();

        snapshot.IsAvailable.ShouldBeFalse();
        snapshot.FailureCount.ShouldBeGreaterThan(0);
        snapshot.SampleCount.ShouldBe(0);
    }

    [Fact]
    public async Task StopAsync_ShouldBeIdempotent()
    {
        await using var sampler = new PostgreSqlLockWaitSampler(factory.DatabaseConnectionString, TimeSpan.FromMilliseconds(10));
        await sampler.StartAsync();
        await Task.Delay(20);

        PostgreSqlLockWaitSnapshot first = await sampler.StopAsync();
        PostgreSqlLockWaitSnapshot second = await sampler.StopAsync();

        second.ShouldBe(first);
    }

    private static async Task WaitForSameAdvisoryLockAsync(
        string connectionString,
        string resourceKey)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@resource_key, 0))", connection);
        command.Parameters.AddWithValue("resource_key", resourceKey);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}

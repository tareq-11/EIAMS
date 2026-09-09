using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Performance;

[Collection(nameof(IntegrationTestCollection))]
public sealed class ApiBenchmarkWorkerProfileTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task FullBackgroundWorkload_RegistersWorkersAndExecutesRecurringCyclesDuringTheWindow()
    {
        var collector = new SqlCommandCounterInterceptor();
        using IntegrationTestWebAppFactory.BenchmarkProfiledWebAppFactory profileFactory =
            factory.CreateSiblingFactory(collector, ApiBenchmarkWorkerProfile.FullBackgroundWorkload);

        // Host creation starts hosted services. The assertion below polls an observable SQL signature,
        // rather than assuming a cycle occurred because a configured delay happened to elapse.
        using HttpClient client = profileFactory.CreateClient();
        IReadOnlySet<Type> hostedWorkers = ApiBenchmarkWorkerProfiles.GetMeasuredHostedWorkerTypes(profileFactory.Services);
        hostedWorkers.SetEquals(ApiBenchmarkWorkerProfiles.MeasuredWorkerTypes).ShouldBeTrue();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (ApiBenchmarkWorkerProfiles.MeasuredWorkerTypes.Any(worker =>
                   ApiBenchmarkWorkerProfiles.CountObservedCycles(collector, worker) < 2))
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
            }
            catch (OperationCanceledException)
            {
                string observed = string.Join(", ", ApiBenchmarkWorkerProfiles.ObservedCycles(collector).Select(type => type.Name));
                throw new InvalidOperationException($"Timed out waiting for worker cycles. Observed: {observed}.");
            }
        }

        ApiBenchmarkWorkerProfiles.MeasuredWorkerTypes.ShouldAllBe(worker =>
            ApiBenchmarkWorkerProfiles.CountObservedCycles(collector, worker) >= 2);
        profileFactory.WorkerProfileMetadata.ShouldBe(ApiBenchmarkWorkerProfiles.GetMetadata(ApiBenchmarkWorkerProfile.FullBackgroundWorkload));
    }

    [Fact]
    public async Task IsolatedRequestCost_RemovesOnlyMeasuredWorkersAndDoesNotExecuteTheirCycles()
    {
        var collector = new SqlCommandCounterInterceptor();
        using IntegrationTestWebAppFactory.BenchmarkProfiledWebAppFactory profileFactory =
            factory.CreateSiblingFactory(collector, ApiBenchmarkWorkerProfile.IsolatedRequestCost);

        using HttpClient client = profileFactory.CreateClient();
        client.BaseAddress = new Uri(client.BaseAddress!, "api/v1/");
        using HttpResponseMessage response = await client.GetAsync("health/ready");
        response.EnsureSuccessStatusCode();

        IReadOnlySet<Type> hostedWorkers = ApiBenchmarkWorkerProfiles.GetMeasuredHostedWorkerTypes(profileFactory.Services);
        hostedWorkers.ShouldBeEmpty();
        ApiBenchmarkWorkerProfiles.ObservedCycles(collector).ShouldBeEmpty();
        profileFactory.WorkerProfileMetadata.ShouldBe(ApiBenchmarkWorkerProfiles.GetMetadata(ApiBenchmarkWorkerProfile.IsolatedRequestCost));
    }

    [Fact]
    public void Profiles_DescribeSeparateArtifactsAndComparableInputs()
    {
        ApiBenchmarkWorkerProfileMetadata full = ApiBenchmarkWorkerProfiles.GetMetadata(ApiBenchmarkWorkerProfile.FullBackgroundWorkload);
        ApiBenchmarkWorkerProfileMetadata isolated = ApiBenchmarkWorkerProfiles.GetMetadata(ApiBenchmarkWorkerProfile.IsolatedRequestCost);

        full.Profile.ShouldNotBe(isolated.Profile);
        full.ComparisonBasis.ShouldContain("equivalent fresh Testcontainer schema");
        full.ComparisonBasis.ShouldContain("physical database instance may differ");
        full.ComparisonBasis.ShouldContain("run order/environment must be recorded");
        full.ComparisonBasis.ShouldContain("worker cadence knobs intentionally differ");
        full.ComparisonBasis.ShouldContain("never combine profile percentiles");
        isolated.PolymorphicReferenceAuditWorkerEnabled.ShouldBeFalse();
        isolated.FileCleanupWorkerEnabled.ShouldBeFalse();
        isolated.IdempotencyCleanupWorkerEnabled.ShouldBeFalse();
    }

    [Fact]
    public void DatasetMetadata_DescribesTheActualLatencyAndSmokeFixturesWithoutIds()
    {
        ApiBenchmarkDatasetMetadata latency = ApiBenchmarkWorkerProfiles.CreateLatencyDatasetMetadata();
        ApiBenchmarkDatasetMetadata smoke = ApiBenchmarkWorkerProfiles.CreateLoadSmokeDatasetMetadata(20260908);

        latency.ShouldBe(new ApiBenchmarkDatasetMetadata(
            "IntegrationFixture", null,
            "Fresh Testcontainer schema migrated by IntegrationTestWebAppFactory with its fixed administrator credentials and role fixture; no SyntheticDatasetSeeder dataset is applied."));
        smoke.ShouldBe(new ApiBenchmarkDatasetMetadata(
            "SyntheticSmall", 20260908,
            "SyntheticDatasetSeeder Small profile plus the IntegrationTestWebAppFactory fixed administrator credentials and role fixture."));
    }

    [Fact]
    public void GetRequestedProfile_InvalidEnvironmentValue_ThrowsAndRestoresTheProcessSetting()
    {
        const string name = "EIAMS_BENCHMARK_WORKER_PROFILE";
        string? original = Environment.GetEnvironmentVariable(name);
        try
        {
            Environment.SetEnvironmentVariable(name, "not-a-profile");
            Should.Throw<InvalidOperationException>(() => ApiBenchmarkWorkerProfiles.GetRequestedProfile())
                .Message.ShouldContain(name);
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, original);
        }
    }
}

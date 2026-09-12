using Infrastructure.Idempotency;
using Infrastructure.PolymorphicReferences;
using Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IntegrationTests.Performance;

internal enum ApiBenchmarkWorkerProfile
{
    FullBackgroundWorkload,
    IsolatedRequestCost
}

internal sealed record ApiBenchmarkWorkerProfileMetadata(
    string Profile,
    bool PolymorphicReferenceAuditWorkerEnabled,
    bool FileCleanupWorkerEnabled,
    bool IdempotencyCleanupWorkerEnabled,
    string PolymorphicReferenceAuditCadence,
    string FileCleanupCadence,
    string IdempotencyCleanupCadence,
    string ComparisonBasis);

internal sealed record ApiBenchmarkDatasetMetadata(string Kind, long? Seed, string Preparation);

internal static class ApiBenchmarkWorkerProfiles
{
    private const string ProfileEnvironmentVariable = "EIAMS_BENCHMARK_WORKER_PROFILE";
    internal static readonly Type[] MeasuredWorkerTypes =
    [
        typeof(PolymorphicReferenceAuditWorker),
        typeof(FileCleanupWorker),
        typeof(IdempotencyCleanupWorker)
    ];

    internal static ApiBenchmarkWorkerProfileMetadata GetMetadata(ApiBenchmarkWorkerProfile profile) =>
        profile == ApiBenchmarkWorkerProfile.FullBackgroundWorkload
            ? new(
                nameof(ApiBenchmarkWorkerProfile.FullBackgroundWorkload), true, true, true,
                "enabled; initial delay 50ms; interval 250ms (test factory override; recurring background overhead included)",
                "enabled; first cycle at host start; poll interval 250ms (test factory override; recurring background overhead included)",
                "enabled; initial delay 50ms; interval 250ms (test factory override; recurring background overhead included)",
                "Compare equivalent fresh Testcontainer schema and fixture preparation, request-path configuration, Kestrel transport, and sample count with IsolatedRequestCost; the physical database instance may differ because profiles run in separate processes, and run order/environment must be recorded; worker cadence knobs intentionally differ; never combine profile percentiles.")
            : new(
                nameof(ApiBenchmarkWorkerProfile.IsolatedRequestCost), false, false, false,
                "removed from this test host", "removed from this test host", "removed from this test host",
                "Compare equivalent fresh Testcontainer schema and fixture preparation, request-path configuration, Kestrel transport, and sample count with FullBackgroundWorkload; the physical database instance may differ because profiles run in separate processes, and run order/environment must be recorded; worker cadence knobs intentionally differ; never combine profile percentiles.");

    internal static ApiBenchmarkDatasetMetadata CreateLatencyDatasetMetadata() => new(
        "IntegrationFixture",
        null,
        "Fresh Testcontainer schema migrated by IntegrationTestWebAppFactory with its fixed administrator credentials and role fixture; no SyntheticDatasetSeeder dataset is applied.");

    internal static ApiBenchmarkDatasetMetadata CreateLoadSmokeDatasetMetadata(long seed) => new(
        "SyntheticSmall",
        seed,
        "SyntheticDatasetSeeder Small profile plus the IntegrationTestWebAppFactory fixed administrator credentials and role fixture.");

    internal static ApiBenchmarkWorkerProfile GetRequestedProfile()
    {
        string? configured = Environment.GetEnvironmentVariable(ProfileEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configured))
        {
            return ApiBenchmarkWorkerProfile.IsolatedRequestCost;
        }

        if (Enum.TryParse(configured, ignoreCase: true, out ApiBenchmarkWorkerProfile profile))
        {
            return profile;
        }

        throw new InvalidOperationException(
            $"{ProfileEnvironmentVariable} must be {nameof(ApiBenchmarkWorkerProfile.FullBackgroundWorkload)} or {nameof(ApiBenchmarkWorkerProfile.IsolatedRequestCost)}.");
    }

    internal static void RemoveMeasuredWorkers(IServiceCollection services)
    {
        for (int index = services.Count - 1; index >= 0; index--)
        {
            ServiceDescriptor descriptor = services[index];
            if (descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType is not null && MeasuredWorkerTypes.Contains(descriptor.ImplementationType))
            {
                services.RemoveAt(index);
            }
        }
    }

    internal static IReadOnlySet<Type> GetMeasuredHostedWorkerTypes(IServiceProvider services) =>
        services.GetServices<IHostedService>()
            .Select(service => service.GetType())
            .Where(MeasuredWorkerTypes.Contains)
            .ToHashSet();

    internal static int CountObservedCycles(SqlCommandCounterInterceptor collector, Type workerType) =>
        collector.GetCommandTexts().Count(commandText => workerType switch
        {
            Type type when type == typeof(PolymorphicReferenceAuditWorker) =>
                commandText.Contains("pg_try_advisory_xact_lock", StringComparison.Ordinal),
            Type type when type == typeof(FileCleanupWorker) =>
                commandText.Contains("pending_file_deletion", StringComparison.OrdinalIgnoreCase),
            Type type when type == typeof(IdempotencyCleanupWorker) =>
                commandText.Contains("DELETE FROM public.idempotency_records", StringComparison.Ordinal),
            _ => false
        });

    internal static bool HasObservedCycle(SqlCommandCounterInterceptor collector, Type workerType) =>
        CountObservedCycles(collector, workerType) > 0;

    internal static IReadOnlySet<Type> ObservedCycles(SqlCommandCounterInterceptor collector) =>
        MeasuredWorkerTypes.Where(workerType => HasObservedCycle(collector, workerType)).ToHashSet();
}

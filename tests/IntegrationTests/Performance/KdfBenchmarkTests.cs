using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Infrastructure.Authentication;
using Xunit.Abstractions;

namespace IntegrationTests.Performance;

public sealed class KdfBenchmarkTests(ITestOutputHelper output)
{
    // This is a fixed test input only. It is never serialized, logged, or used by the application.
    private const string BenchmarkInput = "KdfBenchmarkSyntheticInputOnly";
    private const int SaltSize = 16;
    private const int DerivedKeySize = 32;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    [ExplicitKdfBenchmarkFact]
    [Trait("Category", "Performance")]
    [Trait("WorkloadClass", PerformanceWorkloadContracts.NormalExpectedTraffic)]
    public async Task MeasureBoundedKdfVerificationCostAsync()
    {
        var hasher = new PasswordHasher();
        int currentIterations = ReadIterationsFromCurrentFormat(hasher);
        var configuration = KdfBenchmarkConfiguration.FromEnvironment(currentIterations);
        var results = new List<KdfBenchmarkResult>();

        foreach (KdfBenchmarkCandidate candidate in configuration.Candidates)
        {
            foreach (int concurrency in configuration.ConcurrencyLevels)
            {
                KdfBenchmarkResult result = await MeasureCandidateAsync(candidate, concurrency, configuration);
                results.Add(result);
                output.WriteLine($"{candidate.Label}, iterations={candidate.Iterations}, concurrency={concurrency}: " +
                    $"p50={result.P50Ms:F1}ms, p95={result.P95Ms:F1}ms, wall={result.MeasurementWallClockMilliseconds:F1}ms, " +
                    $"completed_rps={result.CompletedOperationsPerSecond:F2}, cpu={result.ProcessCpuMilliseconds:F1}ms, " +
                    $"working-set-growth={result.WorkingSetGrowthBytes} bytes");
            }
        }

        bool resourcesWithinBudget = results.All(result =>
            result.WorkingSetGrowthBytes <= configuration.MaximumWorkingSetGrowthBytes);
        var artifact = new KdfBenchmarkArtifact(
            resourcesWithinBudget ? "passed" : "failed_resource_safety_budget",
            PerformanceWorkloadContracts.NormalExpectedTraffic,
            new KdfBenchmarkPolicyMetadata(
                "PBKDF2-HMAC-SHA512",
                "The current persisted format is measured first. Any stronger candidate is measurement-only and cannot change production configuration.",
                "Legacy unversioned PBKDF2-SHA512 password verifiers remain readable; a successful login is the only upgrade point.",
                "No candidate below the current emitted work factor is accepted by this harness.",
                configuration.Profile == KdfBenchmarkProfile.QuickValidation
                    ? "QuickValidation validates the harness and its safety controls only; its small sample count is not production profiling evidence."
                    : "FullBaseline is initial KDF profiling evidence only when run on a representative deployment environment; it does not automatically approve a production KDF change."),
            new KdfBenchmarkSafetyBudget(
                configuration.MaximumTotalDerivations,
                configuration.MaximumWorkingSetGrowthBytes,
                configuration.ConcurrencyLevels.Max(),
                "The process working-set delta is a safety signal, not per-operation memory attribution."),
            new KdfBenchmarkEnvironment(
                Environment.Version.ToString(),
                RuntimeInformation.FrameworkDescription,
                RuntimeInformation.OSDescription,
                Environment.ProcessorCount,
                "in-process KDF primitive measurement; excludes HTTP, database, TLS, token issuance, and request rate limits",
                "CPU, allocations, and working-set deltas are process-wide safety signals across each candidate/concurrency window; they are not per-operation attribution."),
            configuration.Profile.ToString(),
            results,
            "Passwords, salts, derived keys, serialized password verifiers, users, emails, identifiers, connection strings, and SQL are intentionally excluded.");

        string path = await KdfBenchmarkArtifactWriter.WriteAsync(configuration.ResultDirectory, artifact);
        output.WriteLine($"KDF benchmark result: {path}");
        resourcesWithinBudget.ShouldBeTrue();
    }

    private static async Task<KdfBenchmarkResult> MeasureCandidateAsync(
        KdfBenchmarkCandidate candidate,
        int concurrency,
        KdfBenchmarkConfiguration configuration)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] expected = Rfc2898DeriveBytes.Pbkdf2(BenchmarkInput, salt, candidate.Iterations, Algorithm, DerivedKeySize);
        await RunConcurrentWorkersAsync(
            concurrency,
            configuration.WarmupOperationsPerWorker,
            (_, _) => VerifyOrThrow(candidate.Iterations, salt, expected));

        long allocationsBefore = GC.GetTotalAllocatedBytes(precise: false);
        using var beforeProcess = Process.GetCurrentProcess();
        TimeSpan cpuBefore = beforeProcess.TotalProcessorTime;
        long workingSetBefore = beforeProcess.WorkingSet64;
        double[] samples = new double[concurrency * configuration.MeasurementOperationsPerWorker];
        TimeSpan measurementWallClock = await RunConcurrentWorkersAsync(
            concurrency,
            configuration.MeasurementOperationsPerWorker,
            (worker, operation) =>
            {
                var stopwatch = Stopwatch.StartNew();
                VerifyOrThrow(candidate.Iterations, salt, expected);
                stopwatch.Stop();
                samples[worker * configuration.MeasurementOperationsPerWorker + operation] = stopwatch.Elapsed.TotalMilliseconds;
            });
        using var afterProcess = Process.GetCurrentProcess();
        long allocatedBytes = GC.GetTotalAllocatedBytes(precise: false) - allocationsBefore;
        long workingSetGrowth = Math.Max(0, afterProcess.WorkingSet64 - workingSetBefore);
        double cpuMilliseconds = Math.Max(0, (afterProcess.TotalProcessorTime - cpuBefore).TotalMilliseconds);
        Array.Sort(samples);

        return KdfBenchmarkMetrics.CreateResult(
            candidate,
            concurrency,
            samples,
            measurementWallClock,
            cpuMilliseconds,
            allocatedBytes,
            workingSetGrowth);
    }

    private static async Task<TimeSpan> RunConcurrentWorkersAsync(
        int concurrency,
        int operationsPerWorker,
        Action<int, int> operation)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(concurrency, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(operationsPerWorker, 1);
        ArgumentNullException.ThrowIfNull(operation);

        using var start = new ManualResetEventSlim(false);
        using var ready = new CountdownEvent(concurrency);
        Task[] workers = Enumerable.Range(0, concurrency).Select(worker => Task.Run(() =>
        {
            ready.Signal();
            start.Wait();
            for (int index = 0; index < operationsPerWorker; index++)
            {
                operation(worker, index);
            }
        })).ToArray();
        ready.Wait();
        var stopwatch = Stopwatch.StartNew();
        start.Set();
        await Task.WhenAll(workers);
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static void VerifyOrThrow(int iterations, byte[] salt, byte[] expected)
    {
        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(BenchmarkInput, salt, iterations, Algorithm, DerivedKeySize);
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            throw new CryptographicException("Synthetic KDF verification unexpectedly failed.");
        }
    }

    private static int ReadIterationsFromCurrentFormat(PasswordHasher hasher)
    {
        string[] parts = hasher.Hash(BenchmarkInput).Split('$');
        if (parts.Length != 4 || !int.TryParse(parts[1], out int iterations) || iterations < 1)
        {
            throw new InvalidOperationException("The current password hasher did not emit a supported versioned format.");
        }

        return iterations;
    }
}

internal sealed record KdfBenchmarkResult(
    string Candidate,
    int Iterations,
    int Concurrency,
    int AttemptedOperations,
    int CompletedOperations,
    double MeasurementWallClockMilliseconds,
    double CompletedOperationsPerSecond,
    double P50Ms,
    double P95Ms,
    double P99Ms,
    double ProcessCpuMilliseconds,
    long AllocatedBytes,
    long WorkingSetGrowthBytes);

internal sealed record KdfBenchmarkArtifact(
    string Status,
    string WorkloadClass,
    KdfBenchmarkPolicyMetadata Policy,
    KdfBenchmarkSafetyBudget SafetyBudget,
    KdfBenchmarkEnvironment Environment,
    string Profile,
    IReadOnlyList<KdfBenchmarkResult> Results,
    string DataHandling);

internal sealed record KdfBenchmarkPolicyMetadata(
    string Algorithm,
    string PromotionRule,
    string CompatibilityRule,
    string DowngradeRule,
    string EvidenceLimit);
internal sealed record KdfBenchmarkSafetyBudget(int MaximumTotalDerivations, long MaximumWorkingSetGrowthBytes, int MaximumConcurrency, string MemoryInterpretation);
internal sealed record KdfBenchmarkEnvironment(
    string DotNetRuntimeVersion,
    string Runtime,
    string OperatingSystem,
    int ProcessorCount,
    string Scope,
    string ResourceMeasurementInterpretation);

internal static class KdfBenchmarkMetrics
{
    internal static KdfBenchmarkResult CreateResult(
        KdfBenchmarkCandidate candidate,
        int concurrency,
        IReadOnlyList<double> samples,
        TimeSpan measurementWallClock,
        double processCpuMilliseconds,
        long allocatedBytes,
        long workingSetGrowthBytes)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentOutOfRangeException.ThrowIfLessThan(concurrency, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(measurementWallClock, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegative(processCpuMilliseconds);
        ArgumentOutOfRangeException.ThrowIfNegative(allocatedBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(workingSetGrowthBytes);
        if (samples.Count == 0 || samples.Any(sample => sample < 0))
        {
            throw new ArgumentException("KDF measurement samples must be present and non-negative.", nameof(samples));
        }

        double[] orderedSamples = samples.Order().ToArray();
        int attemptedOperations = orderedSamples.Length;
        int completedOperations = orderedSamples.Length;
        double wallClockMilliseconds = measurementWallClock.TotalMilliseconds;
        return new(
            candidate.Label,
            candidate.Iterations,
            concurrency,
            attemptedOperations,
            completedOperations,
            wallClockMilliseconds,
            completedOperations / measurementWallClock.TotalSeconds,
            ApiLatencyBenchmarkMetrics.Percentile(orderedSamples, .50) ?? 0,
            ApiLatencyBenchmarkMetrics.Percentile(orderedSamples, .95) ?? 0,
            ApiLatencyBenchmarkMetrics.Percentile(orderedSamples, .99) ?? 0,
            processCpuMilliseconds,
            allocatedBytes,
            workingSetGrowthBytes);
    }
}

internal static class KdfBenchmarkArtifactWriter
{
    internal static async Task<string> WriteAsync(string directory, KdfBenchmarkArtifact artifact, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        Directory.CreateDirectory(directory);
        string fileName = $"kdf-benchmark-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json";
        string destination = Path.Combine(directory, fileName);
        string temporary = Path.Combine(directory, $".{fileName}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(artifact), cancellationToken);
            File.Move(temporary, destination, overwrite: false);
            return destination;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class ExplicitKdfBenchmarkFactAttribute : FactAttribute
{
    public ExplicitKdfBenchmarkFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(KdfBenchmarkConfiguration.GateEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            Skip = "Explicit KDF benchmark. Set RUN_KDF_BENCHMARK=1; FullBaseline additionally requires EIAMS_ALLOW_LONG_KDF_BENCHMARK=1.";
        }
    }
}

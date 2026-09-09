using System.Globalization;

namespace IntegrationTests.Performance;

/// <summary>
/// Explicit, test-only limits for KDF cost measurements. These limits protect a developer or CI
/// machine from accidentally turning a benchmark into an unbounded CPU or memory workload.
/// They are not production password-policy settings.
/// </summary>
internal sealed record KdfBenchmarkConfiguration(
    KdfBenchmarkProfile Profile,
    IReadOnlyList<KdfBenchmarkCandidate> Candidates,
    IReadOnlyList<int> ConcurrencyLevels,
    int WarmupOperationsPerWorker,
    int MeasurementOperationsPerWorker,
    int MaximumTotalDerivations,
    long MaximumWorkingSetGrowthBytes,
    string ResultDirectory)
{
    internal const string GateEnvironmentVariable = "RUN_KDF_BENCHMARK";
    internal const string LongRunOptInEnvironmentVariable = "EIAMS_ALLOW_LONG_KDF_BENCHMARK";

    // This mirrors the parser's accepted upper bound in PasswordHasher. It is a test-workload
    // safety bound, not an OWASP recommendation and not a value emitted to production config.
    private const int MaximumSupportedIterations = 1_000_000;
    private const int MaximumCandidateCount = 2;
    private const long MaximumWorkingSetGrowthLimitBytes = 256L * 1024 * 1024;

    internal static KdfBenchmarkConfiguration FromEnvironment(
        int currentIterations,
        Func<string, string?>? getEnvironmentVariable = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(currentIterations, 1);
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        if (!IsOne(getEnvironmentVariable(GateEnvironmentVariable)))
        {
            throw new InvalidOperationException($"{GateEnvironmentVariable}=1 is required to run the KDF benchmark.");
        }

        KdfBenchmarkProfile profile = ParseEnum(
            getEnvironmentVariable("EIAMS_KDF_BENCHMARK_PROFILE"),
            KdfBenchmarkProfile.QuickValidation,
            "EIAMS_KDF_BENCHMARK_PROFILE");
        if (profile == KdfBenchmarkProfile.FullBaseline && !IsOne(getEnvironmentVariable(LongRunOptInEnvironmentVariable)))
        {
            throw new InvalidOperationException(
                $"{LongRunOptInEnvironmentVariable}=1 is required for the intentionally long FullBaseline profile.");
        }

        int maximumConcurrency = Math.Min(4, Math.Max(1, Environment.ProcessorCount));
        int[] concurrencies = profile == KdfBenchmarkProfile.QuickValidation
            ? [1, Math.Min(2, maximumConcurrency)]
            : Enumerable.Range(1, maximumConcurrency).ToArray();
        concurrencies = concurrencies.Distinct().ToArray();

        List<KdfBenchmarkCandidate> candidates = CreateCandidates(
            currentIterations,
            getEnvironmentVariable("EIAMS_KDF_BENCHMARK_CANDIDATE_ITERATIONS"));
        int warmupOperations = profile == KdfBenchmarkProfile.QuickValidation ? 1 : 3;
        int measurementOperations = profile == KdfBenchmarkProfile.QuickValidation ? 2 : 20;
        int maximumAllowedDerivations = profile == KdfBenchmarkProfile.QuickValidation ? 32 : 1_000;
        string resultDirectory = ParseResultDirectory(getEnvironmentVariable("EIAMS_KDF_BENCHMARK_RESULT_DIR"));

        // Account for every PBKDF2 call: discovering the emitted production format, one
        // synthetic expected value per candidate/concurrency window, then every warmup and
        // measurement operation executed by each worker.
        int requestedWork = 1 +
            candidates.Count * concurrencies.Length +
            candidates.Count * concurrencies.Sum() * (warmupOperations + measurementOperations);
        if (requestedWork > maximumAllowedDerivations)
        {
            throw new InvalidOperationException("The selected KDF benchmark plan exceeds its bounded derivation budget.");
        }

        return new(
            profile,
            candidates,
            concurrencies,
            warmupOperations,
            measurementOperations,
            requestedWork,
            MaximumWorkingSetGrowthLimitBytes,
            resultDirectory);
    }

    private static List<KdfBenchmarkCandidate> CreateCandidates(int currentIterations, string? configuredCandidateIterations)
    {
        if (currentIterations > MaximumSupportedIterations)
        {
            throw new InvalidOperationException("The production password format exceeds the benchmark safety bound.");
        }

        int strongerTentative = Math.Min(MaximumSupportedIterations, checked(currentIterations + currentIterations / 2));
        var candidates = new List<KdfBenchmarkCandidate>
        {
            new("current_production_format", currentIterations)
        };

        if (!string.IsNullOrWhiteSpace(configuredCandidateIterations))
        {
            int candidate = ParseInt(
                configuredCandidateIterations,
                currentIterations,
                MaximumSupportedIterations,
                "EIAMS_KDF_BENCHMARK_CANDIDATE_ITERATIONS");
            if (candidate != currentIterations)
            {
                candidates.Add(new("explicit_stronger_candidate", candidate));
            }
        }
        else if (strongerTentative > currentIterations)
        {
            // This is deliberately only a stronger tentative test candidate derived from the
            // current persisted format. It is not an automatically approved production setting.
            candidates.Add(new("stronger_tentative_candidate", strongerTentative));
        }

        if (candidates.Count > MaximumCandidateCount)
        {
            throw new InvalidOperationException("The KDF benchmark permits at most two candidates per run.");
        }

        return candidates;
    }

    private static T ParseEnum<T>(string? value, T fallback, string name) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (Enum.TryParse(value, ignoreCase: true, out T parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new ArgumentException($"{name} must be one of: {string.Join(", ", Enum.GetNames<T>())}.");
    }

    private static int ParseInt(string value, int minimum, int maximum, string name)
    {
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) &&
            parsed >= minimum && parsed <= maximum)
        {
            return parsed;
        }

        throw new ArgumentException($"{name} must be an integer between the current production cost and the supported maximum.");
    }

    private static string ParseResultDirectory(string? value)
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "eiams-kdf-benchmark-results"));
        string candidate = string.IsNullOrWhiteSpace(value) ? root : Path.GetFullPath(value);
        string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(candidate, root, comparison) && !candidate.StartsWith(rootWithSeparator, comparison))
        {
            throw new ArgumentException("EIAMS_KDF_BENCHMARK_RESULT_DIR must be inside the local temporary eiams-kdf-benchmark-results directory.");
        }

        return candidate;
    }

    private static bool IsOne(string? value) => string.Equals(value, "1", StringComparison.Ordinal);
}

internal enum KdfBenchmarkProfile { QuickValidation, FullBaseline }

/// <summary>Publicly safe identifiers and work factors only; no password, salt, or hash leaves the benchmark process.</summary>
internal sealed record KdfBenchmarkCandidate(string Label, int Iterations);

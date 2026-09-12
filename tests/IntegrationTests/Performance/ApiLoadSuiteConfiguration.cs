namespace IntegrationTests.Performance;

internal enum ApiLoadSuiteProfile { QuickValidation, FullBaseline }

/// <summary>Environment-only configuration for the explicit API load suite.</summary>
internal sealed record ApiLoadSuiteConfiguration(
    ApiLoadSuiteProfile Profile,
    DatasetProfile DatasetProfile,
    long Seed,
    ApiBenchmarkWorkerProfile WorkerProfile,
    string ResultDirectory,
    ApiLoadTestPlan Plan,
    int MaximumStartedRequestsPerPhase,
    int MaximumPostRequestsPerRun)
{
    internal const string GateEnvironmentVariable = "RUN_API_LOAD_SUITE";
    internal const string LongRunOptInEnvironmentVariable = "EIAMS_ALLOW_LONG_LOAD_TEST";

    internal static ApiLoadSuiteConfiguration FromEnvironment(Func<string, string?>? getEnvironmentVariable = null)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        if (!IsOne(getEnvironmentVariable(GateEnvironmentVariable)))
        {
            throw new InvalidOperationException($"{GateEnvironmentVariable}=1 is required to run the API load suite.");
        }

        ApiLoadSuiteProfile profile = ParseEnum(getEnvironmentVariable("EIAMS_API_LOAD_PROFILE"), ApiLoadSuiteProfile.QuickValidation, "EIAMS_API_LOAD_PROFILE");
        if (profile == ApiLoadSuiteProfile.FullBaseline && !IsOne(getEnvironmentVariable(LongRunOptInEnvironmentVariable)))
        {
            throw new InvalidOperationException($"{LongRunOptInEnvironmentVariable}=1 is required for FullBaseline because it is intentionally long-running.");
        }

        DatasetProfile dataset = ParseEnum(getEnvironmentVariable("EIAMS_API_LOAD_DATASET"), DatasetProfile.Small, "EIAMS_API_LOAD_DATASET");
        if (profile == ApiLoadSuiteProfile.QuickValidation && dataset != DatasetProfile.Small)
        {
            throw new InvalidOperationException("QuickValidation only permits the Small dataset.");
        }

        long seed = ParseLong(getEnvironmentVariable("EIAMS_API_LOAD_SEED"), 20260909, 1, long.MaxValue, "EIAMS_API_LOAD_SEED");
        ApiBenchmarkWorkerProfile worker = ParseEnum(getEnvironmentVariable("EIAMS_API_LOAD_WORKER_PROFILE"), ApiBenchmarkWorkerProfile.IsolatedRequestCost, "EIAMS_API_LOAD_WORKER_PROFILE");
        string directory = ParseResultDirectory(getEnvironmentVariable("EIAMS_API_LOAD_RESULT_DIR"));
        int defaultRequestCap = profile == ApiLoadSuiteProfile.QuickValidation ? 500 : 1_000_000;
        int defaultWriteCap = profile == ApiLoadSuiteProfile.QuickValidation ? 50 : 100_000;
        int requestCap = ParseInt(getEnvironmentVariable("EIAMS_API_LOAD_MAX_STARTED_REQUESTS"), defaultRequestCap, 1, 1_000_000, "EIAMS_API_LOAD_MAX_STARTED_REQUESTS");
        int writeCap = ParseInt(getEnvironmentVariable("EIAMS_API_LOAD_MAX_POST_REQUESTS"), defaultWriteCap, 1, 1_000_000, "EIAMS_API_LOAD_MAX_POST_REQUESTS");

        ApiLoadTestConfiguration runConfiguration = profile == ApiLoadSuiteProfile.FullBaseline
            ? ApiLoadTestConfiguration.CreateDefault() with { Seed = (ulong)seed }
            : new ApiLoadTestConfiguration(1, [1, 2], [ApiLoadTestMode.ClosedLoop, ApiLoadTestMode.ConstantArrivalRate],
                TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), 1, (ulong)seed);
        return new(profile, dataset, seed, worker, directory, ApiLoadTestPlan.Create(runConfiguration), requestCap, writeCap);
    }

    internal static string CreateRunNamespace(ApiLoadTestRunDefinition run)
    {
        char mode = run.Mode == ApiLoadTestMode.ClosedLoop ? 'c' : 'a';
        // The random suffix survives the 24-character business-code constraint.
        return $"r{run.RunNumber}-{mode}{run.ClientConcurrency}-{Guid.NewGuid():N}"[..24];
    }

    private static T ParseEnum<T>(string? value, T fallback, string name) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }
        if (Enum.TryParse(value, true, out T parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }
        throw new ArgumentException($"{name} must be one of: {string.Join(", ", Enum.GetNames<T>())}.");
    }

    private static int ParseInt(string? value, int fallback, int min, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }
        if (int.TryParse(value, out int parsed) && parsed >= min && parsed <= max)
        {
            return parsed;
        }
        throw new ArgumentException($"{name} must be an integer between {min} and {max}.");
    }

    private static long ParseLong(string? value, long fallback, long min, long max, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }
        if (long.TryParse(value, out long parsed) && parsed >= min && parsed <= max)
        {
            return parsed;
        }
        throw new ArgumentException($"{name} must be an integer between {min} and {max}.");
    }

    private static string ParseResultDirectory(string? value)
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "eiams-api-load-results"));
        string candidate = string.IsNullOrWhiteSpace(value) ? root : Path.GetFullPath(value);
        string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(candidate, root, comparison) && !candidate.StartsWith(rootWithSeparator, comparison))
        {
            throw new ArgumentException("EIAMS_API_LOAD_RESULT_DIR must be inside the local temporary eiams-api-load-results directory.");
        }
        return candidate;
    }

    private static bool IsOne(string? value) => string.Equals(value, "1", StringComparison.Ordinal);
}

internal static class ApiLoadSuiteArtifactWriter
{
    internal static async Task<string> WriteAsync(string directory, string fileName, object result, CancellationToken cancellationToken = default)
    {
        if (Path.GetFileName(fileName) != fileName || !fileName.EndsWith(".json", StringComparison.Ordinal))
        {
            throw new ArgumentException("Artifact file name must be a simple .json name.", nameof(fileName));
        }
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, fileName);
        string temporary = Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporary, System.Text.Json.JsonSerializer.Serialize(result), cancellationToken);
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

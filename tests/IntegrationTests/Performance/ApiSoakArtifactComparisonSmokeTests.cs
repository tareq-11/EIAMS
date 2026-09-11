namespace IntegrationTests.Performance;

public sealed class ApiSoakArtifactComparisonSmokeTests
{
    [ExplicitApiSoakComparisonFact]
    [Trait("Category", "Performance")]
    public async Task CompareExplicitArtifactPairAsync()
    {
        var configuration = ApiSoakArtifactComparisonConfiguration.FromEnvironment();
        string baseline = await File.ReadAllTextAsync(configuration.BaselineArtifactPath);
        string candidate = await File.ReadAllTextAsync(configuration.CandidateArtifactPath);
        ApiSoakArtifactComparisonResult comparison = ApiSoakArtifactComparison.Compare(baseline, candidate, configuration.Thresholds);
        string path = await ApiSoakArtifactComparison.WriteValidatedAsync(configuration.ResultDirectory,
            $"api-soak-comparison-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.json", comparison);
        Assert.True(File.Exists(path));
        Assert.False(comparison.HasRegression, "Comparison artifact was written, but configured regression thresholds were exceeded.");
    }
}

internal sealed record ApiSoakArtifactComparisonConfiguration(string BaselineArtifactPath, string CandidateArtifactPath,
    string ResultDirectory, ApiSoakComparisonThresholds Thresholds)
{
    internal const string GateEnvironmentVariable = "RUN_API_SOAK_COMPARISON";

    internal static ApiSoakArtifactComparisonConfiguration FromEnvironment(Func<string, string?>? get = null)
    {
        get ??= Environment.GetEnvironmentVariable;
        if (!string.Equals(get(GateEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{GateEnvironmentVariable}=1 is required.");
        }

        string baseline = ParseArtifactPath(get("EIAMS_API_SOAK_BASELINE_ARTIFACT"), "EIAMS_API_SOAK_BASELINE_ARTIFACT");
        string candidate = ParseArtifactPath(get("EIAMS_API_SOAK_CANDIDATE_ARTIFACT"), "EIAMS_API_SOAK_CANDIDATE_ARTIFACT");
        string directory = ParseResultDirectory(get("EIAMS_API_SOAK_COMPARISON_RESULT_DIR"));
        double latencyThreshold = ParsePercentage(get("EIAMS_API_SOAK_LATENCY_REGRESSION_PERCENT"), 10, "EIAMS_API_SOAK_LATENCY_REGRESSION_PERCENT");
        double throughputThreshold = ParsePercentage(get("EIAMS_API_SOAK_THROUGHPUT_REGRESSION_PERCENT"), 10, "EIAMS_API_SOAK_THROUGHPUT_REGRESSION_PERCENT");
        return new(baseline, candidate, directory, new(latencyThreshold, throughputThreshold));
    }

    private static string ParseArtifactPath(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.");
        }
        string path = Path.GetFullPath(value);
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "eiams-api-soak-results"));
        string prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(path, root, comparison) && !path.StartsWith(prefix, comparison) || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            throw new ArgumentException($"{name} must identify an existing JSON artifact.");
        }
        return path;
    }

    private static string ParseResultDirectory(string? value)
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "eiams-api-soak-results"));
        string candidate = string.IsNullOrWhiteSpace(value) ? root : Path.GetFullPath(value);
        string prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(candidate, root, comparison) && !candidate.StartsWith(prefix, comparison))
        {
            throw new ArgumentException("EIAMS_API_SOAK_COMPARISON_RESULT_DIR must be inside the local temporary eiams-api-soak-results directory.");
        }
        return candidate;
    }

    private static double ParsePercentage(string? value, double fallback, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }
        if (double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out double percentage) &&
            double.IsFinite(percentage) && percentage is >= 0 and <= 100)
        {
            return percentage;
        }
        throw new ArgumentException($"{name} must be a finite percentage between zero and one hundred.");
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class ExplicitApiSoakComparisonFactAttribute : FactAttribute
{
    public ExplicitApiSoakComparisonFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(ApiSoakArtifactComparisonConfiguration.GateEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            Skip = "Explicit artifact comparison. Set RUN_API_SOAK_COMPARISON=1 and provide the two artifact paths.";
        }
    }
}

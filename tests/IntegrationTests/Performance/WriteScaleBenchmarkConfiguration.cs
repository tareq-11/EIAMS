namespace IntegrationTests.Performance;

/// <summary>
/// Explicit safety limits for the document-posting scale benchmark. These are test workload caps,
/// not application limits and never permit a connection outside the integration-test container.
/// </summary>
internal sealed record WriteScaleBenchmarkConfiguration(
    WriteScaleBenchmarkProfile Profile,
    IReadOnlyList<WriteScaleScenario> Scenarios,
    int WarmupOperations,
    int MaximumTotalDocumentLines,
    int MaximumTotalInventoryKeys,
    string ResultDirectory)
{
    internal const string GateEnvironmentVariable = "RUN_WRITE_SCALE_BENCHMARK";
    internal const string LongRunOptInEnvironmentVariable = "EIAMS_ALLOW_LONG_WRITE_SCALE_BENCHMARK";
    internal const int MaximumLinesPerScenario = 1_000;
    internal const int MaximumInventoryKeysPerScenario = 100;

    internal static WriteScaleBenchmarkConfiguration FromEnvironment(Func<string, string?>? getEnvironmentVariable = null)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        if (!IsOne(getEnvironmentVariable(GateEnvironmentVariable)))
        {
            throw new InvalidOperationException($"{GateEnvironmentVariable}=1 is required to run the write-scale benchmark.");
        }

        WriteScaleBenchmarkProfile profile = ParseProfile(getEnvironmentVariable("EIAMS_WRITE_SCALE_BENCHMARK_PROFILE"));
        if (profile == WriteScaleBenchmarkProfile.Scale && !IsOne(getEnvironmentVariable(LongRunOptInEnvironmentVariable)))
        {
            throw new InvalidOperationException(
                $"{LongRunOptInEnvironmentVariable}=1 is required for Scale because it creates up to 1,000 posted lines.");
        }

        IReadOnlyList<WriteScaleScenario> scenarios = profile == WriteScaleBenchmarkProfile.QuickValidation
            ? [new("one_line_one_inventory_key", 1, 1)]
            :
            [
                new("one_line_one_inventory_key", 1, 1),
                new("hundred_lines_one_inventory_key", 100, 1),
                new("hundred_lines_ten_inventory_keys", 100, 10),
                new("thousand_lines_one_inventory_key", 1_000, 1),
                new("thousand_lines_hundred_inventory_keys", 1_000, 100)
            ];

        ValidateScenarios(scenarios);
        return new(
            profile,
            scenarios,
            WarmupOperations: 1,
            MaximumTotalDocumentLines: profile == WriteScaleBenchmarkProfile.QuickValidation ? 2 : 2_202,
            MaximumTotalInventoryKeys: profile == WriteScaleBenchmarkProfile.QuickValidation ? 2 : 222,
            ParseResultDirectory(getEnvironmentVariable("EIAMS_WRITE_SCALE_BENCHMARK_RESULT_DIR")));
    }

    internal static void ValidateScenarios(IReadOnlyList<WriteScaleScenario> scenarios)
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        if (scenarios.Count == 0 || scenarios.Any(s => string.IsNullOrWhiteSpace(s.Name) || s.LineCount < 1 ||
                s.LineCount > MaximumLinesPerScenario || s.InventoryKeyCount < 1 ||
                s.InventoryKeyCount > MaximumInventoryKeysPerScenario || s.InventoryKeyCount > s.LineCount))
        {
            throw new ArgumentException("Scenarios must have bounded non-empty names, 1..1000 lines, and 1..100 inventory keys no greater than lines.", nameof(scenarios));
        }
    }

    private static WriteScaleBenchmarkProfile ParseProfile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return WriteScaleBenchmarkProfile.QuickValidation;
        }
        if (Enum.TryParse(value, ignoreCase: true, out WriteScaleBenchmarkProfile profile) && Enum.IsDefined(profile))
        {
            return profile;
        }
        throw new ArgumentException($"EIAMS_WRITE_SCALE_BENCHMARK_PROFILE must be one of: {string.Join(", ", Enum.GetNames<WriteScaleBenchmarkProfile>())}.");
    }

    private static string ParseResultDirectory(string? value)
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "eiams-write-scale-benchmark-results"));
        string candidate = string.IsNullOrWhiteSpace(value) ? root : Path.GetFullPath(value);
        string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(candidate, root, comparison) && !candidate.StartsWith(rootWithSeparator, comparison))
        {
            throw new ArgumentException("EIAMS_WRITE_SCALE_BENCHMARK_RESULT_DIR must be inside the local temporary eiams-write-scale-benchmark-results directory.");
        }
        return candidate;
    }

    private static bool IsOne(string? value) => string.Equals(value, "1", StringComparison.Ordinal);
}

internal enum WriteScaleBenchmarkProfile { QuickValidation, Scale }
internal sealed record WriteScaleScenario(string Name, int LineCount, int InventoryKeyCount);

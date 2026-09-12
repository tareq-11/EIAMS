using System.Text.Json;
using System.Text.RegularExpressions;

namespace IntegrationTests.Performance;

internal enum ApiSoakSuiteProfile { QuickValidation, FullSoak }

/// <summary>Environment-only configuration for a deliberately explicit, bounded soak run.</summary>
internal sealed record ApiSoakSuiteConfiguration(
    ApiSoakSuiteProfile Profile, DatasetProfile DatasetProfile, long Seed,
    ApiBenchmarkWorkerProfile WorkerProfile, string ResultDirectory,
    ApiLoadTestRunDefinition Run, int MaximumStartedRequests, int MaximumPostRequests,
    double? RequestsPerSecond)
{
    internal const string GateEnvironmentVariable = "RUN_API_SOAK_SUITE";
    internal const string LongRunOptInEnvironmentVariable = "EIAMS_ALLOW_LONG_SOAK_TEST";

    internal static ApiSoakSuiteConfiguration FromEnvironment(Func<string, string?>? get = null)
    {
        get ??= Environment.GetEnvironmentVariable;
        if (!IsOne(get(GateEnvironmentVariable)))
        {
            throw new InvalidOperationException($"{GateEnvironmentVariable}=1 is required.");
        }
        ApiSoakSuiteProfile profile = ParseEnum(get("EIAMS_API_SOAK_PROFILE"), ApiSoakSuiteProfile.QuickValidation, "EIAMS_API_SOAK_PROFILE");
        if (profile == ApiSoakSuiteProfile.FullSoak && !IsOne(get(LongRunOptInEnvironmentVariable)))
        {
            throw new InvalidOperationException($"{LongRunOptInEnvironmentVariable}=1 is required for FullSoak.");
        }
        string? configuredDataset = get("EIAMS_API_SOAK_DATASET");
        if (profile == ApiSoakSuiteProfile.FullSoak && string.IsNullOrWhiteSpace(configuredDataset))
        {
            throw new ArgumentException("EIAMS_API_SOAK_DATASET must be explicitly supplied for FullSoak.");
        }

        DatasetProfile dataset = ParseEnum(configuredDataset, DatasetProfile.Small, "EIAMS_API_SOAK_DATASET");
        if (profile == ApiSoakSuiteProfile.QuickValidation && dataset != DatasetProfile.Small)
        {
            throw new ArgumentException("QuickValidation only permits the Small dataset.");
        }
        if (profile == ApiSoakSuiteProfile.FullSoak && dataset == DatasetProfile.Small)
        {
            throw new ArgumentException("FullSoak requires a representative Medium or Large dataset.");
        }
        long seed = ParseLong(get("EIAMS_API_SOAK_SEED"), 20260911, "EIAMS_API_SOAK_SEED");
        TimeSpan measurement = profile == ApiSoakSuiteProfile.QuickValidation
            ? TimeSpan.FromSeconds(ParseInt(get("EIAMS_API_SOAK_DURATION_SECONDS"), 12, 10, 20, "EIAMS_API_SOAK_DURATION_SECONDS"))
            : TimeSpan.FromMinutes(ParseInt(get("EIAMS_API_SOAK_DURATION_MINUTES"), 60, 60, 120, "EIAMS_API_SOAK_DURATION_MINUTES"));
        int concurrency = profile == ApiSoakSuiteProfile.QuickValidation
            ? ParseInt(get("EIAMS_API_SOAK_CONCURRENCY"), 2, 1, 4, "EIAMS_API_SOAK_CONCURRENCY")
            : ParseInt(get("EIAMS_API_SOAK_CONCURRENCY"), 4, 1, 64, "EIAMS_API_SOAK_CONCURRENCY");
        double? requestsPerSecond = profile == ApiSoakSuiteProfile.FullSoak
            ? ParseRequiredRate(get("EIAMS_API_SOAK_REQUESTS_PER_SECOND"))
            : null;
        int requestCap = ParseInt(get("EIAMS_API_SOAK_MAX_STARTED_REQUESTS"), profile == ApiSoakSuiteProfile.QuickValidation ? 10_000 : 1_000_000, 1, 1_000_000, "EIAMS_API_SOAK_MAX_STARTED_REQUESTS");
        int writeCap = ParseInt(get("EIAMS_API_SOAK_MAX_POST_REQUESTS"), profile == ApiSoakSuiteProfile.QuickValidation ? 1_000 : 100_000, 1, 1_000_000, "EIAMS_API_SOAK_MAX_POST_REQUESTS");
        if (profile == ApiSoakSuiteProfile.FullSoak)
        {
            int requiredRequests = RequiredCount(measurement, requestsPerSecond!.Value, 1.10);
            int requiredWrites = RequiredCount(measurement, requestsPerSecond.Value * .10, 1.10);
            if (requestCap < requiredRequests || writeCap < requiredWrites)
            {
                throw new ArgumentException("FullSoak request and write caps must cover the configured duration, rate, and workload mix with scheduling margin.");
            }
        }
        var run = new ApiLoadTestRunDefinition(1, concurrency,
            profile == ApiSoakSuiteProfile.FullSoak ? ApiLoadTestMode.ConstantArrivalRate : ApiLoadTestMode.ClosedLoop,
            requestsPerSecond, (ulong)seed,
            new ApiLoadTestPhase(ApiLoadTestPhaseKind.Warmup, TimeSpan.FromSeconds(1)),
            new ApiLoadTestPhase(ApiLoadTestPhaseKind.Measurement, measurement),
            [new(ApiLoadTestScenario.ReadList, 45), new(ApiLoadTestScenario.ReadDetail, 25), new(ApiLoadTestScenario.Report, 20), new(ApiLoadTestScenario.Post, 10)]);
        return new(profile, dataset, seed,
            ParseEnum(get("EIAMS_API_SOAK_WORKER_PROFILE"), ApiBenchmarkWorkerProfile.IsolatedRequestCost, "EIAMS_API_SOAK_WORKER_PROFILE"),
            ParseResultDirectory(get("EIAMS_API_SOAK_RESULT_DIR")), run, requestCap, writeCap, requestsPerSecond);
    }

    internal static string CreateRunNamespace() => $"soak-{Guid.NewGuid():N}"[..24];
    private static bool IsOne(string? value) => string.Equals(value, "1", StringComparison.Ordinal);
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

        throw new ArgumentException($"{name} must be a valid enum value.");
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

        throw new ArgumentException($"{name} must be between {min} and {max}.");
    }
    private static long ParseLong(string? value, long fallback, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (long.TryParse(value, out long parsed) && parsed > 0)
        {
            return parsed;
        }

        throw new ArgumentException($"{name} must be positive.");
    }
    private static double ParseRequiredRate(string? value)
    {
        if (!double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out double rate) || rate is < 1 or > 100)
        {
            throw new ArgumentException("EIAMS_API_SOAK_REQUESTS_PER_SECOND must be explicitly supplied between 1 and 100.");
        }
        return rate;
    }
    private static int RequiredCount(TimeSpan duration, double rate, double margin)
    {
        double count = Math.Ceiling(duration.TotalSeconds * rate * margin);
        return count > 1_000_000 ? throw new ArgumentException("Configured soak workload exceeds the absolute request cap.") : (int)count;
    }
    private static string ParseResultDirectory(string? value)
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "eiams-api-soak-results"));
        string candidate = string.IsNullOrWhiteSpace(value) ? root : Path.GetFullPath(value);
        string prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(candidate, root, comparison) && !candidate.StartsWith(prefix, comparison))
        {
            throw new ArgumentException("EIAMS_API_SOAK_RESULT_DIR must be inside the local temporary eiams-api-soak-results directory.");
        }
        return candidate;
    }
}

internal static partial class ApiSoakArtifactSafetyValidator
{
    [GeneratedRegex(@"(?i)(password|secret|token|authorization|connection[ _-]?string)", RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveTerm();
    [GeneratedRegex(@"(?i)(?:https?://|\b[a-z][a-z0-9+.-]*://|[?&][a-z0-9_-]+=[^\s]*|(?:^|\s)/(?:[a-z0-9._~-]+/?)+|\b[a-z]:[\\/])", RegexOptions.CultureInvariant)]
    private static partial Regex UnsafePayload();
    [GeneratedRegex(@"(?is)\bselect\b[^\r\n]{0,512}\bfrom\b|\binsert\b[^\r\n]{0,512}\binto\b|\bupdate\b[^\r\n]{0,512}\bset\b|\bdelete\b[^\r\n]{0,512}\bfrom\b", RegexOptions.CultureInvariant)]
    private static partial Regex SqlStatement();
    [GeneratedRegex(@"(?i)(?:^|[_-])(?:url|path|query|raw_sql|sql_text)(?:$|[_-])", RegexOptions.CultureInvariant)]
    private static partial Regex UnsafeFieldName();
    [GeneratedRegex(@"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b", RegexOptions.CultureInvariant)]
    private static partial Regex GuidValue();
    [GeneratedRegex(@"(?i)\b[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex EmailValue();

    internal static void EnsureSafe(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        Visit(document.RootElement);
    }

    private static void Visit(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (SensitiveTerm().IsMatch(property.Name) || UnsafeFieldName().IsMatch(property.Name))
                    {
                        throw new ArgumentException("Soak artifacts must not contain sensitive field names.");
                    }
                    Visit(property.Value);
                }
                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    Visit(item);
                }

                break;
            case JsonValueKind.String:
                string value = element.GetString() ?? string.Empty;
                if (SensitiveTerm().IsMatch(value) || UnsafePayload().IsMatch(value) || SqlStatement().IsMatch(value) || GuidValue().IsMatch(value) || EmailValue().IsMatch(value))
                {
                    throw new ArgumentException("Soak artifacts must contain only safe aggregate values.");
                }
                break;
        }
    }
}

internal static class ApiSoakArtifactWriter
{
    internal static async Task<string> WriteValidatedAsync(string directory, string fileName, object artifact,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ApiSoakArtifactSafetyValidator.EnsureSafe(JsonSerializer.Serialize(artifact));
        }
        catch (ArgumentException)
        {
            // Never leave a nominally passed artifact behind when its content failed safety review.
            await ApiLoadSuiteArtifactWriter.WriteAsync(directory, fileName,
                new { Status = "failed", FailureKind = "ArtifactSafetyValidation", RetainedRequestSamples = 0 },
                cancellationToken).ConfigureAwait(false);
            throw;
        }

        string path = await ApiLoadSuiteArtifactWriter.WriteAsync(directory, fileName, artifact, cancellationToken)
            .ConfigureAwait(false);
        string persistedJson = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        ApiSoakArtifactSafetyValidator.EnsureSafe(persistedJson);
        return path;
    }
}

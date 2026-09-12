namespace IntegrationTests.Performance;

public sealed class ApiSoakArtifactComparisonConfigurationTests
{
    [Fact]
    public void FromEnvironment_RequiresGate()
    {
        Should.Throw<InvalidOperationException>(() => ApiSoakArtifactComparisonConfiguration.FromEnvironment(_ => null));
    }

    [Fact]
    public void FromEnvironment_RejectsMissingPathsAndInvalidThreshold()
    {
        Should.Throw<ArgumentException>(() => ApiSoakArtifactComparisonConfiguration.FromEnvironment(Name => Name == "RUN_API_SOAK_COMPARISON" ? "1" : null));
        Should.Throw<ArgumentException>(() => ApiSoakArtifactComparisonConfiguration.FromEnvironment(Name => Name switch
        {
            "RUN_API_SOAK_COMPARISON" => "1",
            "EIAMS_API_SOAK_BASELINE_ARTIFACT" => "/tmp/not-an-artifact.json",
            _ => null
        }));
    }

    [Fact]
    public void FromEnvironment_RejectsOutsideRootAndAcceptsBoundedValidConfiguration()
    {
        string root = Path.Combine(Path.GetTempPath(), "eiams-api-soak-results", Guid.NewGuid().ToString("N"));
        string baseline = Path.Combine(root, "baseline.json");
        string candidate = Path.Combine(root, "candidate.json");
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(baseline, "{}");
            File.WriteAllText(candidate, "{}");
            Should.Throw<ArgumentException>(() => ApiSoakArtifactComparisonConfiguration.FromEnvironment(Name => Name switch
            {
                "RUN_API_SOAK_COMPARISON" => "1",
                "EIAMS_API_SOAK_BASELINE_ARTIFACT" => "/tmp/outside.json",
                "EIAMS_API_SOAK_CANDIDATE_ARTIFACT" => candidate,
                _ => null
            }));
            Should.Throw<ArgumentException>(() => ApiSoakArtifactComparisonConfiguration.FromEnvironment(Name => Name switch
            {
                "RUN_API_SOAK_COMPARISON" => "1",
                "EIAMS_API_SOAK_BASELINE_ARTIFACT" => baseline,
                "EIAMS_API_SOAK_CANDIDATE_ARTIFACT" => candidate,
                "EIAMS_API_SOAK_LATENCY_REGRESSION_PERCENT" => "101",
                _ => null
            }));

            var configuration = ApiSoakArtifactComparisonConfiguration.FromEnvironment(Name => Name switch
            {
                "RUN_API_SOAK_COMPARISON" => "1",
                "EIAMS_API_SOAK_BASELINE_ARTIFACT" => baseline,
                "EIAMS_API_SOAK_CANDIDATE_ARTIFACT" => candidate,
                "EIAMS_API_SOAK_LATENCY_REGRESSION_PERCENT" => "12.5",
                "EIAMS_API_SOAK_THROUGHPUT_REGRESSION_PERCENT" => "7.5",
                _ => null
            });
            configuration.Thresholds.ShouldBe(new ApiSoakComparisonThresholds(12.5, 7.5));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

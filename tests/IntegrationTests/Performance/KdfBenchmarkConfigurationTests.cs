using System.Security.Cryptography;
using Infrastructure.Authentication;

namespace IntegrationTests.Performance;

public sealed class KdfBenchmarkConfigurationTests
{
    [Fact]
    public void FromEnvironment_RequiresGateAndKeepsDefaultCandidateAtOrAboveCurrentCost()
    {
        Should.Throw<InvalidOperationException>(() => KdfBenchmarkConfiguration.FromEnvironment(500_000, _ => null));

        var configuration = KdfBenchmarkConfiguration.FromEnvironment(
            500_000,
            name => name == "RUN_KDF_BENCHMARK" ? "1" : null);

        configuration.Profile.ShouldBe(KdfBenchmarkProfile.QuickValidation);
        configuration.Candidates.ShouldContain(candidate => candidate.Label == "current_production_format" && candidate.Iterations == 500_000);
        configuration.Candidates.ShouldAllBe(candidate => candidate.Iterations >= 500_000);
        configuration.ConcurrencyLevels.Max().ShouldBeLessThanOrEqualTo(Math.Min(4, Math.Max(1, Environment.ProcessorCount)));
        configuration.MaximumTotalDerivations.ShouldBe(
            1 + configuration.Candidates.Count * configuration.ConcurrencyLevels.Count +
            configuration.Candidates.Count * configuration.ConcurrencyLevels.Sum() *
            (configuration.WarmupOperationsPerWorker + configuration.MeasurementOperationsPerWorker));
    }

    [Fact]
    public void FromEnvironment_RejectsUnsafeCandidateAndLongProfileWithoutSecondGate()
    {
        Should.Throw<ArgumentException>(() => KdfBenchmarkConfiguration.FromEnvironment(500_000, name => name switch
        {
            "RUN_KDF_BENCHMARK" => "1", "EIAMS_KDF_BENCHMARK_CANDIDATE_ITERATIONS" => "499999", _ => null
        }));
        Should.Throw<InvalidOperationException>(() => KdfBenchmarkConfiguration.FromEnvironment(500_000, name => name switch
        {
            "RUN_KDF_BENCHMARK" => "1", "EIAMS_KDF_BENCHMARK_PROFILE" => "FullBaseline", _ => null
        }));
    }

    [Fact]
    public void FromEnvironment_AcceptsOnlyStrongerExplicitCandidateAndContainedArtifactDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "eiams-kdf-benchmark-results");
        string child = Path.Combine(root, "candidate");
        var configuration = KdfBenchmarkConfiguration.FromEnvironment(500_000, name => name switch
        {
            "RUN_KDF_BENCHMARK" => "1",
            "EIAMS_KDF_BENCHMARK_CANDIDATE_ITERATIONS" => "750000",
            "EIAMS_KDF_BENCHMARK_RESULT_DIR" => child,
            _ => null
        });

        configuration.Candidates.ShouldContain(candidate => candidate.Label == "explicit_stronger_candidate" && candidate.Iterations == 750_000);
        configuration.ResultDirectory.ShouldBe(Path.GetFullPath(child));
        Should.Throw<ArgumentException>(() => KdfBenchmarkConfiguration.FromEnvironment(500_000, name => name switch
        {
            "RUN_KDF_BENCHMARK" => "1", "EIAMS_KDF_BENCHMARK_RESULT_DIR" => root + "-outside", _ => null
        }));
    }

    [Fact]
    public void PasswordHasher_PreservesLegacyVerificationAndRejectsMalformedOrTooExpensiveFormats()
    {
        const string password = "compatibility-test-only";
        var hasher = new PasswordHasher();
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] derived = Rfc2898DeriveBytes.Pbkdf2(password, salt, 500_000, HashAlgorithmName.SHA512, 32);
        string legacy = $"{Convert.ToHexString(derived)}-{Convert.ToHexString(salt)}";

        hasher.Verify(password, legacy).ShouldBeTrue();
        hasher.NeedsRehash(legacy).ShouldBeTrue();
        hasher.Verify(password, "pbkdf2-sha512$1000001$0000000000000000000000000000000000000000000000000000000000000000$00000000000000000000000000000000").ShouldBeFalse();
        hasher.Verify(password, "not-a-hash").ShouldBeFalse();
    }

    [Fact]
    public async Task ArtifactWriter_ExcludesSensitiveValuesAndCleansTemporaryFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), "eiams-kdf-benchmark-results", $"test-{Guid.NewGuid():N}");
        try
        {
            var artifact = new KdfBenchmarkArtifact(
                "passed", "NormalExpectedTraffic",
                new("PBKDF2-HMAC-SHA512", "manual", "legacy", "no downgrade", "validation only"),
                new(8, 1024, 1, "aggregate"),
                new("runtime-version", "runtime", "os", 1, "KDF only", "process-wide"),
                KdfBenchmarkProfile.QuickValidation.ToString(),
                [new("current", 500_000, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1)],
                "redacted");
            string path = await KdfBenchmarkArtifactWriter.WriteAsync(directory, artifact);
            string json = await File.ReadAllTextAsync(path);

            json.ShouldNotContain("compatibility-test-only");
            json.ShouldNotContain("@", Case.Insensitive);
            json.ShouldNotContain("password", Case.Insensitive);
            json.ShouldNotContain("hash", Case.Insensitive);
            Directory.GetFiles(directory, "*.tmp").ShouldBeEmpty();
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Metrics_ReportsMeasurementWindowAndCompletedThroughput()
    {
        KdfBenchmarkResult result = KdfBenchmarkMetrics.CreateResult(
            new("current", 500_000),
            2,
            [10d, 20d, 30d, 40d],
            TimeSpan.FromSeconds(2),
            120,
            1024,
            2048);

        result.AttemptedOperations.ShouldBe(4);
        result.CompletedOperations.ShouldBe(4);
        result.MeasurementWallClockMilliseconds.ShouldBe(2000);
        result.CompletedOperationsPerSecond.ShouldBe(2);
        result.P95Ms.ShouldBe(40);
    }
}

using Infrastructure.Database;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Application.UnitTests.Database;

public sealed class DatabaseConnectionOptionsValidatorTests
{
    private readonly DatabaseConnectionOptionsValidator validator = new();

    [Fact]
    public void Validate_ShouldAcceptDefaultConnectionBudget()
    {
        ValidateOptionsResult result = validator.Validate(null, new DatabaseConnectionOptions());

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldRejectRequestTimeoutThatCannotOutliveSqlCancellation()
    {
        ValidateOptionsResult result = validator.Validate(null, new DatabaseConnectionOptions
        {
            CommandTimeoutSeconds = 25,
            CancellationTimeoutSeconds = 2,
            RequestTimeoutSeconds = 26
        });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("RequestTimeoutSeconds");
    }

    [Fact]
    public void Validate_ShouldRejectConnectionBudgetExhaustedByReplicas()
    {
        ValidateOptionsResult result = validator.Validate(null, new DatabaseConnectionOptions
        {
            MaxPoolSize = 20,
            ApplicationReplicaCount = 3,
            DatabaseConnectionBudget = 50
        });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("connection budget");
    }

    [Fact]
    public void Validate_ShouldRejectUnpooledRuntimeBecauseItsConnectionBudgetCannotBeEnforced()
    {
        ValidateOptionsResult result = validator.Validate(null, new DatabaseConnectionOptions { Pooling = false });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Pooling must remain enabled");
    }

    [Fact]
    public void Validate_ShouldReportFailuresForExtremeValuesWithoutOverflowing()
    {
        ValidateOptionsResult result = validator.Validate(null, new DatabaseConnectionOptions
        {
            MaxPoolSize = int.MaxValue,
            ApplicationReplicaCount = int.MaxValue,
            HealthCheckConnectionReserve = int.MaxValue,
            MigrationConnectionReserve = int.MaxValue
        });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("MaxPoolSize");
    }

    [Theory]
    [InlineData("Host=ep-example-123.eu-central-1.aws.neon.tech;Database=neondb", DatabaseEndpointKind.NeonDirect)]
    [InlineData("Host=ep-example-123-pooler.eu-central-1.aws.neon.tech;Database=neondb", DatabaseEndpointKind.NeonPooled)]
    [InlineData("Host=localhost;Database=neondb", DatabaseEndpointKind.Other)]
    public void Classify_ShouldDistinguishDirectAndPooledNeonWithoutReadingCredentials(
        string connectionString,
        DatabaseEndpointKind expected)
    {
        DatabaseEndpointClassifier.Classify(connectionString).ShouldBe(expected);
    }

    [Fact]
    public void ValidateExpectedMode_ShouldRejectDirectNeonWhenPooledWasRequired()
    {
        Should.Throw<InvalidOperationException>(() => DatabaseEndpointClassifier.ValidateExpectedMode(
            "Host=ep-example-123.eu-central-1.aws.neon.tech;Database=neondb",
            DatabaseEndpointMode.Pooled));
    }

    [Theory]
    [InlineData("Host=ep-example-123.eu-central-1.aws.neon.tech;Database=neondb", DatabaseEndpointMode.Direct)]
    [InlineData("Host=ep-example-123-pooler.eu-central-1.aws.neon.tech;Database=neondb", DatabaseEndpointMode.Pooled)]
    [InlineData("Host=localhost;Database=neondb", DatabaseEndpointMode.Direct)]
    public void ValidateExpectedMode_ShouldAcceptMatchingOrLocalEndpoint(
        string connectionString,
        DatabaseEndpointMode mode)
    {
        Should.NotThrow(() => DatabaseEndpointClassifier.ValidateExpectedMode(connectionString, mode));
    }

    [Fact]
    public void MergeTimeouts_ShouldPreserveExistingStartupOptionsAndAppendAuthoritativeLimits()
    {
        string merged = PostgresStartupOptions.MergeTimeouts(
            "-c application_name=eiams-api -c statement_timeout=1",
            commandTimeoutSeconds: 25,
            lockTimeoutSeconds: 20);

        merged.ShouldBe(
            "-c application_name=eiams-api -c statement_timeout=1 -c statement_timeout=25000 -c lock_timeout=20000");
    }

    [Fact]
    public void ApplyTimeouts_ShouldNotCopyConnectionSecretsIntoPostgresOptions()
    {
        var builder = new NpgsqlConnectionStringBuilder(
            "Host=localhost;Database=eiams;Username=application;Password=must-not-appear-in-options;Options=-c application_name=eiams-api");

        PostgresStartupOptions.ApplyTimeouts(builder, new DatabaseConnectionOptions());

        string startupOptions = builder.Options ?? throw new InvalidOperationException("Expected startup options.");
        startupOptions.ShouldContain("application_name=eiams-api");
        startupOptions.ShouldContain("statement_timeout=25000");
        startupOptions.ShouldContain("lock_timeout=20000");
        startupOptions.ShouldNotContain("must-not-appear-in-options");
        startupOptions.ShouldNotContain("Password");
    }
}

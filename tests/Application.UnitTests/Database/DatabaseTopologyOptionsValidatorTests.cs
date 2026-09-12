using Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Application.UnitTests.Database;

public sealed class DatabaseTopologyOptionsValidatorTests
{
    private readonly DatabaseTopologyOptionsValidator validator = new();

    [Fact]
    public void Validate_ShouldAcceptPrimaryOnlyTopology()
    {
        ValidateOptionsResult result = validator.Validate(null, new DatabaseTopologyOptions());

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFailClosedWhenAReadReplicaIsConfigured()
    {
        ValidateOptionsResult result = validator.Validate(null, new DatabaseTopologyOptions
        {
            ReadReplicaConnectionString = "Host=replica.example.test;Database=eiams"
        });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("scope", Case.Insensitive);
        result.FailureMessage.ShouldContain("stale-tolerant reporting", Case.Insensitive);
    }

    [Fact]
    public void Validate_ShouldFailClosedWhenRlsIsEnabledWithoutSessionIsolationDesign()
    {
        ValidateOptionsResult result = validator.Validate(null, new DatabaseTopologyOptions
        {
            EnableRowLevelSecurity = true
        });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("tenant/session-context", Case.Insensitive);
        result.FailureMessage.ShouldContain("pool reset", Case.Insensitive);
    }

    [Theory]
    [InlineData("DatabaseTopology:ReadReplicaConnectionString", "Host=replica-secret.example.test;Password=must-not-leak")]
    [InlineData("ConnectionStrings:ReadReplica", "Host=replica-secret.example.test;Password=must-not-leak")]
    public void Configuration_ShouldRejectBothReplicaAliasesWithoutEchoingTheConfiguredValue(string key, string value)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })
            .Build();

        OptionsValidationException exception = Should.Throw<OptionsValidationException>(
            () => DatabaseTopologyConfiguration.Validate(configuration));

        exception.Message.ShouldNotContain("replica-secret.example.test");
        exception.Message.ShouldNotContain("must-not-leak");
    }

    [Fact]
    public void Configuration_ShouldRejectConventionalReplicaWhenThePrimaryAliasIsWhitespace()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseTopology:ReadReplicaConnectionString"] = "   ",
                ["ConnectionStrings:ReadReplica"] = "Host=replica-secret.example.test;Password=must-not-leak"
            })
            .Build();

        OptionsValidationException exception = Should.Throw<OptionsValidationException>(
            () => DatabaseTopologyConfiguration.Validate(configuration));

        exception.Message.ShouldContain("ReadReplicaConnectionString");
        exception.Message.ShouldNotContain("replica-secret.example.test");
        exception.Message.ShouldNotContain("must-not-leak");
    }

    [Fact]
    public void Configuration_ShouldAllowPrimaryOnlyStartup()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();

        Should.NotThrow(() => DatabaseTopologyConfiguration.Validate(configuration));
    }
}

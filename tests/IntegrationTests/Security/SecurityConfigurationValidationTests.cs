using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Web.Api.Extensions;

namespace IntegrationTests.Security;

public sealed class SecurityConfigurationValidationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("api.example.test;*")]
    [InlineData("0.0.0.0")]
    [InlineData("[::]")]
    [InlineData("https://api.example.test")]
    [InlineData("api.example.test/path")]
    [InlineData("api.example.test;")]
    public void Production_Should_RejectNonExplicitAllowedHosts(string allowedHosts)
    {
        IConfiguration configuration = BuildConfiguration(allowedHosts);

        Should.Throw<InvalidOperationException>(() =>
            configuration.ValidateProductionSecurityConfiguration(new TestHostEnvironment("Production")));
    }

    [Theory]
    [InlineData("api.example.test")]
    [InlineData("api.example.test;admin-api.example.test")]
    [InlineData("api.example.test:8443")]
    public void Production_Should_AcceptExplicitAllowedHosts(string allowedHosts)
    {
        IConfiguration configuration = BuildConfiguration(allowedHosts);

        Should.NotThrow(() =>
            configuration.ValidateProductionSecurityConfiguration(new TestHostEnvironment("Production")));
    }

    [Fact]
    public void Development_Should_AllowDevelopmentWildcard()
    {
        IConfiguration configuration = BuildConfiguration("*");

        Should.NotThrow(() =>
            configuration.ValidateProductionSecurityConfiguration(new TestHostEnvironment("Development")));
    }

    private static IConfiguration BuildConfiguration(string allowedHosts) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AllowedHosts"] = allowedHosts
            })
            .Build();

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "SecurityConfigurationTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Web.Api.Extensions;

namespace IntegrationTests.Security;

public sealed class ForwardedHeadersConfigurationTests
{
    [Fact]
    public void EmptyTrustedPeerConfiguration_Should_DisableForwardedHeaders()
    {
        ForwardedHeadersOptions options = BuildOptions(new Dictionary<string, string?>());

        options.ForwardedHeaders.ShouldBe(ForwardedHeaders.None);
        options.KnownProxies.ShouldBeEmpty();
        options.KnownIPNetworks.ShouldBeEmpty();
    }

    [Fact]
    public void ExplicitTrustedProxy_Should_EnableForwardedHeadersForOneHop()
    {
        var values = new Dictionary<string, string?>
        {
            ["ForwardedHeaders:KnownProxies:0"] = "10.0.0.10"
        };

        ForwardedHeadersOptions options = BuildOptions(values);

        options.ForwardedHeaders.ShouldBe(
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        options.ForwardLimit.ShouldBe(1);
        options.KnownProxies.Single().ToString().ShouldBe("10.0.0.10");
    }

    [Fact]
    public void InvalidTrustedProxy_Should_FailConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            ["ForwardedHeaders:KnownProxies:0"] = "not-an-ip-address"
        };

        Should.Throw<InvalidOperationException>(() => BuildOptions(values));
    }

    private static ForwardedHeadersOptions BuildOptions(
        IReadOnlyDictionary<string, string?> values)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddForwardedHeaders(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
    }
}

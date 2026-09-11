using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Web.Api.Extensions;

namespace IntegrationTests.Security;

public sealed class HealthRateLimitingConfigurationTests
{
    [Theory]
    [InlineData("RateLimiting:Health:PermitLimit")]
    [InlineData("RateLimiting:Health:WindowInSeconds")]
    [InlineData("RateLimiting:Health:ConcurrencyLimit")]
    public void HealthRateLimitingSettings_ShouldRejectNonPositiveValues(string key)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = "0" })
            .Build();
        var services = new ServiceCollection();

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => services.AddRateLimitingInternal(configuration));

        exception.Message.ShouldContain(key);
    }

    [Fact]
    public void HealthRateLimitPartition_ShouldBeDistinctForDifferentClientAddresses()
    {
        var first = new DefaultHttpContext();
        first.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.0.2.10");
        var second = new DefaultHttpContext();
        second.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.0.2.11");

        string firstPartition = RateLimitingExtensions.GetPartitionKey(first);
        string secondPartition = RateLimitingExtensions.GetPartitionKey(second);

        firstPartition.ShouldBe("ip:192.0.2.10");
        secondPartition.ShouldBe("ip:192.0.2.11");
        secondPartition.ShouldNotBe(firstPartition);
    }
}

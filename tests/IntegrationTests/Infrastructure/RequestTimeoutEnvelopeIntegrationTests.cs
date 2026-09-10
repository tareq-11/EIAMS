using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Web.Api;

namespace IntegrationTests.Infrastructure;

[Collection(nameof(IntegrationTestCollection))]
public sealed class RequestTimeoutEnvelopeIntegrationTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task TimedOutRequest_ShouldReturnTheStandardErrorEnvelope()
    {
        using WebApplicationFactory<Program> timeoutFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DatabasePerformance:ConnectionTimeoutSeconds", "1");
            builder.UseSetting("DatabasePerformance:CommandTimeoutSeconds", "1");
            builder.UseSetting("DatabasePerformance:LockTimeoutSeconds", "1");
            builder.UseSetting("DatabasePerformance:CancellationTimeoutSeconds", "1");
            builder.UseSetting("DatabasePerformance:RequestTimeoutSeconds", "2");
            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter, RequestTimeoutProbeStartupFilter>());
        });
        using HttpClient client = timeoutFactory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/test/request-timeout-probe");

        response.StatusCode.ShouldBe(HttpStatusCode.GatewayTimeout);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("success").GetBoolean().ShouldBeFalse();
        body.RootElement.GetProperty("error").GetProperty("code").GetString().ShouldBe("REQUEST_TIMEOUT");
        body.RootElement.GetProperty("error").GetProperty("message").GetString().ShouldNotBeNullOrWhiteSpace();
        body.RootElement.GetProperty("error").GetProperty("request_id").GetString().ShouldNotBeNullOrWhiteSpace();
    }
}

/// <summary>Test-assembly-only branch used to prove the timeout middleware's public error contract.</summary>
public sealed class RequestTimeoutProbeStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.Map("/api/v1/test/request-timeout-probe", branch => branch.Run(async context =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), context.RequestAborted);
        }));
    };
}

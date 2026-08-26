using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Web.Api.Middleware;

namespace IntegrationTests.Observability;

[Collection(nameof(IntegrationTestCollection))]
public sealed class LoggingTests : BaseIntegrationTest
{
    public LoggingTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task HttpRequest_Should_IncludeCorrelationHeaders()
    {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, "health");
        request.Headers.Add("Correlation-Id", correlationId);

        // Act
        HttpResponseMessage response = await HttpClient.SendAsync(request);

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        response.Headers.TryGetValues("X-Request-Id", out IEnumerable<string>? values).ShouldBeTrue();
        values.ShouldNotBeNull();
        values.Single().ShouldBe(correlationId);
    }

    [Fact]
    public async Task RequestContextMiddleware_Should_KeepCorrelationIdUntilAsyncPipelineCompletes()
    {
        // Arrange
        var sink = new CollectingSink();
        using Logger logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();
        string correlationId = Guid.NewGuid().ToString();
        var context = new DefaultHttpContext();
        context.Request.Headers["Correlation-Id"] = correlationId;
        var middleware = new RequestContextLoggingMiddleware(async _ =>
        {
            await Task.Yield();
            logger.Information("Asynchronous request body completed");
        });

        // Act
        await middleware.Invoke(context);

        // Assert
        LogEvent logEvent = sink.Events.ShouldHaveSingleItem();
        logEvent.Properties["CorrelationId"].ShouldBe(new ScalarValue(correlationId));
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}

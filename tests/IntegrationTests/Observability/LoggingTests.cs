using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Web.Api.Extensions;
using Web.Api.Infrastructure;
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

    [Fact]
    public async Task GlobalExceptionHandler_ShouldNotExposeSensitiveExceptionContent()
    {
        // Arrange
        const string secret = "do-not-log-or-return-this-token";
        var sink = new CollectingSink();
        using Logger logger = new LoggerConfiguration()
            .WriteTo.Sink(sink)
            .CreateLogger();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(logging => logging.AddSerilog(logger));
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddLogging()
            .AddOptions()
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = serviceProvider };
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(loggerFactory.CreateLogger<GlobalExceptionHandler>());

        // Act
        bool handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException($"The supplied token was {secret}"),
            CancellationToken.None);
        context.Response.Body.Position = 0;
        using var responseReader = new StreamReader(context.Response.Body);
        string responseBody = await responseReader.ReadToEndAsync();

        // Assert
        handled.ShouldBeTrue();
        responseBody.ShouldNotContain(secret);
        LogEvent logEvent = sink.Events.ShouldHaveSingleItem();
        logEvent.Exception.ShouldBeNull();
        logEvent.RenderMessage(System.Globalization.CultureInfo.InvariantCulture).ShouldNotContain(secret);
    }

    [Fact]
    public void SensitiveTelemetryRedactionProcessor_ShouldKeepSafePathsAndRemoveSensitiveValues()
    {
        // Arrange
        const string secret = "do-not-export-this-secret";
        using Activity activity = new("telemetry");
        activity.Start();
        activity.SetTag("url.full", $"https://api.test/items?access_token={secret}");
        activity.SetTag("http.url", $"https://api.test/items?password={secret}");
        activity.SetTag("url.query", $"access_token={secret}");
        activity.SetTag("http.target", $"/items?access_token={secret}#fragment");
        activity.SetTag("db.query.text", $"SELECT * FROM users WHERE password = '{secret}'");
        activity.SetTag("db.statement", $"SELECT '{secret}'");
        using SensitiveTelemetryRedactionProcessor processor = new();

        // Act
        processor.OnEnd(activity);

        // Assert
        string exportedTags = string.Join(",", activity.Tags.Select(tag => $"{tag.Key}={tag.Value}"));
        exportedTags.ShouldNotContain(secret);
        activity.GetTagItem("url.full").ShouldBe("https://api.test/items");
        activity.GetTagItem("http.url").ShouldBe("https://api.test/items");
        activity.GetTagItem("url.query").ShouldBeNull();
        activity.GetTagItem("http.target").ShouldBe("/items");
        activity.GetTagItem("db.query.text").ShouldBeNull();
        activity.GetTagItem("db.statement").ShouldBeNull();
    }

    [Fact]
    public void SensitiveTelemetryRedactionProcessor_ShouldRemoveMalformedOrUnsafeUrlTags()
    {
        // Arrange
        const string secret = "do-not-export-this-secret";
        using Activity activity = new("telemetry");
        activity.Start();
        activity.SetTag("url.full", $"not-a-url?access_token={secret}");
        activity.SetTag("http.url", $"ftp://api.test/items?password={secret}");
        activity.SetTag("url.query", $"access_token={secret}");
        activity.SetTag("http.target", $"//api.test/items?access_token={secret}");
        using SensitiveTelemetryRedactionProcessor processor = new();

        // Act
        processor.OnEnd(activity);

        // Assert
        string exportedTags = string.Join(",", activity.Tags.Select(tag => $"{tag.Key}={tag.Value}"));
        exportedTags.ShouldNotContain(secret);
        activity.GetTagItem("url.full").ShouldBeNull();
        activity.GetTagItem("http.url").ShouldBeNull();
        activity.GetTagItem("url.query").ShouldBeNull();
        activity.GetTagItem("http.target").ShouldBeNull();
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}

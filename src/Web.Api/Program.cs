using System.IO.Compression;
using Application;
using Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.ResponseCompression;
using Serilog;
using Web.Api;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Configuration.ValidateProductionSecurityConfiguration(builder.Environment);

builder.Host.UseSerilog((context, loggerConfig) => loggerConfig.ReadFrom.Configuration(context.Configuration));

builder.Services.AddSwaggerGenWithAuth();

builder.Services
    .AddApplication(options => options.SlowHandlerThresholdMilliseconds =
        builder.Configuration.GetValue<int?>(
            $"{Application.Abstractions.Behaviors.PerformanceMonitoringOptions.SectionName}:SlowHandlerThresholdMilliseconds")
        ?? options.SlowHandlerThresholdMilliseconds)
    .AddPresentation(builder.Configuration)
    .AddInfrastructure(builder.Configuration);

builder.Services.AddObservability(builder.Configuration, builder.Environment.ApplicationName);

builder.Services.AddRateLimitingInternal(builder.Configuration);

builder.Services.AddCorsPolicy(builder.Configuration, builder.Environment);

builder.Services.AddForwardedHeaders(builder.Configuration);

int requestTimeoutSeconds = builder.Configuration.GetValue<int?>("DatabasePerformance:RequestTimeoutSeconds") ?? 30;
builder.Services.AddRequestTimeouts(options =>
{
    options.DefaultPolicy = new RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(requestTimeoutSeconds),
        TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
        WriteTimeoutResponse = async context =>
        {
            IResult result = ApiResults.ErrorFromStatusCode(context, StatusCodes.Status504GatewayTimeout);
            await result.ExecuteAsync(context);
        }
    };
});

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerWithUi();

    app.ApplyMigrations();
}

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.MapHealthChecks("api/v1/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});

app.MapHealthChecks("api/v1/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live")
});

app.MapHealthChecks("api/v1/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});

app.UseRequestContextLogging();

app.UseSerilogRequestLogging();

app.UseExceptionHandler();

app.UseRequestTimeouts();

app.UseSecurityHeaders();

app.UseStatusCodePages(async statusCodeContext =>
{
    IResult result = ApiResults.ErrorFromStatusCode(
        statusCodeContext.HttpContext,
        statusCodeContext.HttpContext.Response.StatusCode);

    await result.ExecuteAsync(statusCodeContext.HttpContext);
});

app.UseCors(CorsExtensions.PolicyName);

app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api/v1/auth"),
    branch => branch.UseResponseCompression());

app.UseAuthentication();

app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();

// REMARK: Required for functional and integration tests to work.
namespace Web.Api
{
    public partial class Program;
}

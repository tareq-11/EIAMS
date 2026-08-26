using System.IO.Compression;
using Application;
using HealthChecks.UI.Client;
using Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.ResponseCompression;
using Serilog;
using Web.Api;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfig) => loggerConfig.ReadFrom.Configuration(context.Configuration));

builder.Services.AddSwaggerGenWithAuth();

builder.Services
    .AddApplication()
    .AddPresentation(builder.Configuration)
    .AddInfrastructure(builder.Configuration);

builder.Services.AddObservability(builder.Configuration, builder.Environment.ApplicationName);

builder.Services.AddRateLimitingInternal(builder.Configuration);

builder.Services.AddCorsPolicy(builder.Configuration);

builder.Services.AddForwardedHeaders(builder.Configuration);

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

app.MapHealthChecks("health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.UseForwardedHeaders();

app.UseRequestContextLogging();

app.UseSerilogRequestLogging();

app.UseExceptionHandler();

app.UseStatusCodePages(async statusCodeContext =>
{
    IResult result = ApiResults.ErrorFromStatusCode(
        statusCodeContext.HttpContext,
        statusCodeContext.HttpContext.Response.StatusCode);

    await result.ExecuteAsync(statusCodeContext.HttpContext);
});

app.UseCors(CorsExtensions.PolicyName);

app.UseResponseCompression();

app.UseAuthentication();

app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

await app.RunAsync();

// REMARK: Required for functional and integration tests to work.
namespace Web.Api
{
    public partial class Program;
}

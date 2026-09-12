using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Web.Api.Extensions;

namespace IntegrationTests.Security;

public sealed class SecurityOutcomeMetricsTests
{
    [Fact]
    public void FinalSecurityOutcomes_AndAdministratorOperations_UseOnlyFixedSafeTags()
    {
        var measurements = new ConcurrentBag<Measurement>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "EIAMS.SecurityOperations")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add(new Measurement(instrument.Name, value, tags.ToArray())));
        listener.Start();

        SecurityOutcomeMetricsExtensions.RecordFinalSecurityOutcome(StatusCodes.Status401Unauthorized);
        SecurityOutcomeMetricsExtensions.RecordFinalSecurityOutcome(StatusCodes.Status403Forbidden);
        SecurityOutcomeMetricsExtensions.RecordFinalSecurityOutcome(StatusCodes.Status429TooManyRequests);
        SecurityOutcomeMetricsExtensions.RecordOperation("administrator_bootstrap", "succeeded");
        SecurityOutcomeMetricsExtensions.RecordOperation("administrator_recovery", "rejected");
        SecurityOutcomeMetricsExtensions.RecordOperation("administrator_role_scope_replace", "failed");

        measurements.Any(item => item is
        {
            InstrumentName: "eiam.security.http.outcomes", Value: 1,
            Tags: [{ Key: "outcome", Value: "unauthorized" }]
        }).ShouldBeTrue();
        measurements.Any(item => item is
        {
            InstrumentName: "eiam.security.http.outcomes", Value: 1,
            Tags: [{ Key: "outcome", Value: "forbidden" }]
        }).ShouldBeTrue();
        measurements.Any(item => item is
        {
            InstrumentName: "eiam.security.http.outcomes", Value: 1,
            Tags: [{ Key: "outcome", Value: "rate_limited" }]
        }).ShouldBeTrue();
        measurements.Any(item => item.InstrumentName == "eiam.security.operations" &&
            item.Tags.SequenceEqual([new("event_type", "administrator_bootstrap"), new("outcome", "succeeded")])).ShouldBeTrue();
        measurements.Any(item => item.InstrumentName == "eiam.security.operations" &&
            item.Tags.SequenceEqual([new("event_type", "administrator_recovery"), new("outcome", "rejected")])).ShouldBeTrue();
        measurements.Any(item => item.InstrumentName == "eiam.security.operations" &&
            item.Tags.SequenceEqual([new("event_type", "administrator_role_scope_replace"), new("outcome", "failed")])).ShouldBeTrue();

        measurements.SelectMany(item => item.Tags).Select(tag => tag.Key).Distinct()
            .ShouldBe(["event_type", "outcome"], ignoreOrder: true);
        measurements.SelectMany(item => item.Tags).Select(tag => tag.Value).OfType<string>()
            .ShouldAllBe(value => IsSafeValue(value));
    }

    [Fact]
    public void AlertContract_ShouldReferenceRegisteredSignalsWithAnUnambiguousDisabledSchema()
    {
        string path = Path.Combine(FindRepositoryRoot(), "config", "observability-alert-contract.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;

        root.GetProperty("disabled").GetBoolean().ShouldBeTrue();
        root.TryGetProperty("enabled", out _).ShouldBeFalse();
        JsonElement[] signals = root.GetProperty("signals").EnumerateArray().ToArray();
        signals.All(HasCompleteSignalSchema).ShouldBeTrue();

        signals.ShouldContain(signal =>
            signal.GetProperty("source").GetString() == SecurityOutcomeMetricsExtensions.MeterName &&
            signal.GetProperty("signal").GetString() == SecurityOutcomeMetricsExtensions.OutcomesInstrumentName);
        signals.ShouldContain(signal =>
            signal.GetProperty("source").GetString() == SecurityOutcomeMetricsExtensions.MeterName &&
            signal.GetProperty("signal").GetString() == SecurityOutcomeMetricsExtensions.OperationsInstrumentName);
        signals.ShouldContain(signal =>
            signal.GetProperty("source").GetString() == "CleanArchitecture.Application.Authentication" &&
            signal.GetProperty("signal").GetString() == "security.refresh.events");
        signals.ShouldContain(signal =>
            signal.GetProperty("source").GetString() == NpgsqlMetricTagPolicyExtensions.MeterName &&
            signal.GetProperty("signal").GetString() == NpgsqlMetricTagPolicyExtensions.ConnectionCount);
    }

    [Fact]
    public void NpgsqlMetricPolicy_ShouldExcludeTopologyAndPoolIdentifiers()
    {
        NpgsqlMetricTagPolicyExtensions.AllowedTagKeys(NpgsqlMetricTagPolicyExtensions.ConnectionCount)
            .ShouldBe(["db.client.connection.state"]);

        NpgsqlMetricTagPolicyExtensions.SupportedInstruments.Count.ShouldBe(11);
        foreach (string instrument in NpgsqlMetricTagPolicyExtensions.SupportedInstruments)
        {
            IReadOnlyList<string> allowed = NpgsqlMetricTagPolicyExtensions.AllowedTagKeys(instrument);
            allowed.ShouldNotContain("server.address");
            allowed.ShouldNotContain("server.port");
            allowed.ShouldNotContain("db.namespace");
            allowed.ShouldNotContain("pool.name");
        }
    }

    [Fact]
    public void AlertContract_ShouldContainNoSecretsPiiOrRequestDerivedMetadata()
    {
        string json = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "config", "observability-alert-contract.json"));
        string normalized = json.ToUpperInvariant();

        Regex.IsMatch(json, @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b", RegexOptions.IgnoreCase)
            .ShouldBeFalse();
        normalized.Contains('@').ShouldBeFalse();
        normalized.Contains("://").ShouldBeFalse();
        Regex.IsMatch(normalized, @"\b(PASSWORD|SECRET|TOKEN|AUTHORIZATION|CONNECTION[_-]?STRING)\b")
            .ShouldBeFalse();

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("signals").EnumerateArray()
            .SelectMany(signal => signal.GetProperty("labels").EnumerateArray())
            .Select(label => label.GetString() ?? string.Empty)
            .ShouldAllBe(label => IsSafeContractLabel(label));
    }

    private static bool IsSafeValue(string value) =>
        !value.Contains('@') && !value.Contains('/') && !Guid.TryParse(value, out _);

    private static bool HasCompleteSignalSchema(JsonElement signal) =>
        signal.TryGetProperty("kind", out _) &&
        signal.TryGetProperty("source", out _) &&
        signal.TryGetProperty("signal", out _) &&
        signal.TryGetProperty("labels", out _) &&
        signal.GetProperty("threshold").ValueKind == JsonValueKind.Null;

    private static bool IsSafeContractLabel(string label) =>
        label != "url" && label != "path" && label != "query" && label != "email" &&
        label != "user.id" && label != "client.address";

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CleanArchitectureTemplate.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root for alert-contract verification.");
    }

    private sealed record Measurement(string InstrumentName, long Value, KeyValuePair<string, object?>[] Tags);
}

[Collection(nameof(IntegrationTestCollection))]
public sealed class SecurityOutcomeMetricsPipelineTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task Pipeline_ShouldRecordFinalStatusOutcomes_AndHandledAdministratorFailure()
    {
        using WebApplicationFactory<Program> probeFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, SecurityOutcomeProbeStartupFilter>()));
        using HttpClient client = probeFactory.CreateClient();
        var measurements = new ConcurrentBag<Measurement>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "EIAMS.SecurityOperations")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add(new Measurement(instrument.Name, value, tags.ToArray())));
        listener.Start();

        (await client.GetAsync("/api/v1/test/security-outcome/401")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/test/security-outcome/403")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/v1/test/security-outcome/429")).StatusCode.ShouldBe((HttpStatusCode)429);
        (await client.GetAsync("/api/v1/test/admin-throws")).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        HttpResponseMessage bootstrap = await client.PostAsJsonAsync("/api/v1/admin/users/register", new
        {
            email = "metrics-probe@example.test",
            firstName = "Metrics",
            lastName = "Probe",
            password = "Metrics123!"
        });
        bootstrap.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        measurements.Any(item => item.InstrumentName == "eiam.security.http.outcomes" &&
            item.Tags.SequenceEqual([new("outcome", "unauthorized")])).ShouldBeTrue();
        measurements.Any(item => item.InstrumentName == "eiam.security.http.outcomes" &&
            item.Tags.SequenceEqual([new("outcome", "forbidden")])).ShouldBeTrue();
        measurements.Any(item => item.InstrumentName == "eiam.security.http.outcomes" &&
            item.Tags.SequenceEqual([new("outcome", "rate_limited")])).ShouldBeTrue();
        measurements.Any(item => item.InstrumentName == "eiam.security.operations" &&
            item.Tags.SequenceEqual([new("event_type", "administrator_user_create"), new("outcome", "failed")])).ShouldBeTrue();
        measurements.Any(item => item.InstrumentName == "eiam.security.operations" &&
            item.Tags.SequenceEqual([new("event_type", "administrator_bootstrap"), new("outcome", "rejected")])).ShouldBeTrue();
    }

    private sealed record Measurement(string InstrumentName, long Value, KeyValuePair<string, object?>[] Tags);
}

/// <summary>Assembly-local branches that exercise the deployed middleware order, not recorder helpers.</summary>
public sealed class SecurityOutcomeProbeStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        // Route matching normally supplies controller metadata before security middleware.
        // This branch is not endpoint-routed, so give it a real controller descriptor and a
        // throwing endpoint delegate before the production pipeline starts.
        app.Use(async (context, nextMiddleware) =>
        {
            if (context.Request.Path == "/api/v1/test/admin-throws")
            {
                context.SetEndpoint(new Endpoint(
                    _ => Task.FromException(new InvalidOperationException("test failure")),
                    new EndpointMetadataCollection(new ControllerActionDescriptor
                    {
                        ControllerTypeInfo = typeof(CreateUserController).GetTypeInfo()
                    }),
                    "test-admin-throws"));
            }

            await nextMiddleware(context);
        });
        next(app);
        app.Map("/api/v1/test/security-outcome/401", branch => branch.Run(context => SetStatusAsync(context, 401)));
        app.Map("/api/v1/test/security-outcome/403", branch => branch.Run(context => SetStatusAsync(context, 403)));
        app.Map("/api/v1/test/security-outcome/429", branch => branch.Run(context => SetStatusAsync(context, 429)));
    };

    private static Task SetStatusAsync(HttpContext context, int status)
    {
        context.Response.StatusCode = status;
        return Task.CompletedTask;
    }
}

public sealed class CreateUserController
{
}

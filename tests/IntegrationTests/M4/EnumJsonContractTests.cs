using System.Text.Json;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Web.Api;

namespace IntegrationTests.M4;

public sealed class EnumJsonContractTests
{
    private sealed record OpeningRequest(OpeningType OpeningType);

    [Fact]
    public void ApiJsonOptions_Should_ReadEnumNames()
    {
        JsonSerializerOptions options = CreateApiJsonOptions();

        OpeningRequest? request = JsonSerializer.Deserialize<OpeningRequest>(
            """{"openingType":"Initial"}""",
            options);

        request.ShouldNotBeNull();
        request.OpeningType.ShouldBe(OpeningType.Initial);
    }

    [Fact]
    public void ApiJsonOptions_Should_RejectNumericEnums()
    {
        JsonSerializerOptions options = CreateApiJsonOptions();

        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<OpeningRequest>(
            """{"openingType":0}""",
            options));
    }

    [Fact]
    public void ApiJsonOptions_Should_RejectUnknownEnumNames()
    {
        JsonSerializerOptions options = CreateApiJsonOptions();

        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<OpeningRequest>(
            """{"openingType":"Unknown"}""",
            options));
    }

    private static JsonSerializerOptions CreateApiJsonOptions()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder().Build();

        services.AddLogging();
        services.AddPresentation(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
    }
}

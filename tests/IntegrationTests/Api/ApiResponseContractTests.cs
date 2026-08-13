using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace IntegrationTests.Api;

public sealed class ApiResponseContractTests(IntegrationTestWebAppFactory factory)
    : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task SuccessfulResponse_Should_ContainDataAndRequestMetadata()
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/register",
            new
            {
                email = UniqueEmail(),
                firstName = "Contract",
                lastName = "Tester",
                password = "Password123!"
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using JsonDocument body = await ReadJsonAsync(response);
        JsonElement root = body.RootElement;

        root.GetProperty("success").GetBoolean().ShouldBeTrue();
        root.GetProperty("data").GetProperty("id").GetGuid().ShouldNotBe(Guid.Empty);
        root.GetProperty("pagination").ValueKind.ShouldBe(JsonValueKind.Null);
        root.GetProperty("meta").GetProperty("request_id").GetString().ShouldNotBeNullOrWhiteSpace();
        root.GetProperty("meta").GetProperty("timestamp").GetDateTime().ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
    }

    [Fact]
    public async Task InvalidRequest_Should_ReturnStructuredValidationDetails()
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/register",
            new
            {
                email = "not-an-email",
                firstName = "",
                lastName = "",
                password = "weak"
            });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using JsonDocument body = await ReadJsonAsync(response);
        JsonElement root = body.RootElement;
        JsonElement error = root.GetProperty("error");

        root.GetProperty("success").GetBoolean().ShouldBeFalse();
        error.GetProperty("code").GetString().ShouldBe("VALIDATION_GENERAL");
        error.GetProperty("message").GetString().ShouldNotBeNullOrWhiteSpace();
        error.GetProperty("details").EnumerateObject().ShouldNotBeEmpty();
        error.GetProperty("request_id").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UnauthorizedRequest_Should_ReturnStructuredAuthenticationError()
    {
        HttpResponseMessage response = await HttpClient.GetAsync("warehouses");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using JsonDocument body = await ReadJsonAsync(response);
        JsonElement error = body.RootElement.GetProperty("error");

        body.RootElement.GetProperty("success").GetBoolean().ShouldBeFalse();
        error.GetProperty("code").GetString().ShouldBe("AUTHENTICATION_REQUIRED");
        error.GetProperty("details").ValueKind.ShouldBe(JsonValueKind.Object);
        error.GetProperty("request_id").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UnknownRoute_Should_ReturnStructuredNotFoundError()
    {
        HttpResponseMessage response = await HttpClient.GetAsync($"missing-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using JsonDocument body = await ReadJsonAsync(response);
        JsonElement error = body.RootElement.GetProperty("error");

        body.RootElement.GetProperty("success").GetBoolean().ShouldBeFalse();
        error.GetProperty("code").GetString().ShouldBe("RESOURCE_NOT_FOUND");
        error.GetProperty("message").GetString().ShouldNotBeNullOrWhiteSpace();
        error.GetProperty("request_id").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task DuplicateResource_Should_ReturnStructuredConflictError()
    {
        string email = UniqueEmail();
        await RegisterUserAsync(email);

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/register",
            new
            {
                email,
                firstName = "Duplicate",
                lastName = "User",
                password = "Password123!"
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using JsonDocument body = await ReadJsonAsync(response);
        JsonElement error = body.RootElement.GetProperty("error");

        body.RootElement.GetProperty("success").GetBoolean().ShouldBeFalse();
        error.GetProperty("code").GetString().ShouldNotBeNullOrWhiteSpace();
        error.GetProperty("details").ValueKind.ShouldBe(JsonValueKind.Object);
        error.GetProperty("request_id").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        string contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        contentType.ShouldBe("application/json");

        Stream stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}

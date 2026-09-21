using System.Text;
using System.Text.Json;
using Application.Users;
using Application.Users.GetSession;
using Microsoft.AspNetCore.Hosting;
using Web.Api.Infrastructure;

namespace IntegrationTests.Api;

[Collection(nameof(IntegrationTestCollection))]
public sealed class AuthResponseContractTests
{
    private readonly IntegrationTestWebAppFactory factory;

    public AuthResponseContractTests(IntegrationTestWebAppFactory factory) => this.factory = factory;

    [Fact]
    public void RefreshTransport_Should_ProjectRequiredSession_AndJwtExpiration()
    {
        long expiration = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { exp = expiration })))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string token = $"eyJhbGciOiJub25lIn0.{payload}.signature";
        var session = new UserSessionResponse(
            new UserSessionUserDto(Guid.NewGuid(), "user@example.com", "Test", "User", null, null),
            new UserSessionRoleDto(Guid.NewGuid(), "Administrator", null),
            new UserSessionScopeDto("Enterprise", null, "Enterprise"), "Selected", [], []);
        var transport = new RefreshTokenTransport(new RefreshTokenTransportOptions());

        AuthenticationTokensResponse response = transport.CreateResponse(new AccessTokensResponse(token, "refresh", Guid.NewGuid(), session));

        response.Session.ShouldBeSameAs(session);
        response.ExpiresInSeconds.ShouldBeInRange(298, 300);
    }

    [Theory]
    [InlineData("LoginController-RequestBody")]
    [InlineData("CreateUserController-RequestBody")]
    [InlineData("UpdateUserController-RequestBody")]
    [InlineData("RecoverAdministratorController-RequestBody")]
    public async Task GeneratedSwagger_ShouldMarkUsernameRequired(string requestSchemaSuffix)
    {
        using HttpClient client = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development")).CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        JsonElement schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        JsonElement request = FindSchema(schemas, requestSchemaSuffix);
        JsonElement authResponse = FindSchema(schemas, "AuthenticationTokensResponse");
        request.GetProperty("required").EnumerateArray().Select(item => item.GetString())
            .ShouldContain("username");
        authResponse.GetProperty("required").EnumerateArray().Select(item => item.GetString())
            .ShouldContain("session");
        authResponse.GetProperty("required").EnumerateArray().Select(item => item.GetString())
            .ShouldContain("expiresInSeconds");
    }

    private static JsonElement FindSchema(JsonElement schemas, string suffix) =>
        schemas.EnumerateObject()
            .First(item => item.Name.EndsWith(suffix, StringComparison.Ordinal))
            .Value;
}

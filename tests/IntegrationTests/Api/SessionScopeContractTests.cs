using System.Text.Json;
using Domain.Roles;
using Microsoft.AspNetCore.Hosting;

namespace IntegrationTests.Api;

/// <summary>
/// Locks the D-SRS-01 singular session contract as published on the wire.
/// <para>
/// The session projection is returned through <c>Results.Ok</c>, which serializes with the
/// minimal-API <c>JsonOptions</c> rather than the MVC options the global
/// <c>JsonStringEnumConverter</c> is registered on. An enum without an explicit converter is
/// therefore written as its numeric value, which contradicted the OpenAPI document's declared
/// <c>{"type":"string","enum":["Enterprise","Site","Warehouse"]}</c> and broke every client
/// generated from that document. These tests fail if that divergence is reintroduced.
/// </para>
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public sealed class SessionScopeContractTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public SessionScopeContractTests(IntegrationTestWebAppFactory factory)
        : base(factory) => this.factory = factory;

    [Fact]
    public async Task Session_Should_SerializeActiveScopeTypeAsItsStringEnumValue()
    {
        await AuthenticateAsAdministratorAsync();

        using HttpResponseMessage response = await HttpClient.GetAsync("auth/session");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement scopeType = document.RootElement
            .GetProperty("data")
            .GetProperty("activeScope")
            .GetProperty("scopeType");

        scopeType.ValueKind.ShouldBe(
            JsonValueKind.String,
            "activeScope.scopeType must be a string because the OpenAPI document declares a string enum.");
        scopeType.GetString().ShouldBe("Enterprise");
    }

    [Fact]
    public async Task Session_Should_ExposeSingularRoleScopeAndPermissions_AndNoAssignmentCollection()
    {
        await AuthenticateAsAdministratorAsync();

        using HttpResponseMessage response = await HttpClient.GetAsync("auth/session");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement data = document.RootElement.GetProperty("data");

        data.GetProperty("role").GetProperty("id").GetGuid().ShouldBe(WellKnownRoles.AdministratorId);
        data.GetProperty("role").GetProperty("name").ValueKind.ShouldBe(JsonValueKind.String);
        data.GetProperty("activeScope").GetProperty("scopeName").ValueKind.ShouldBe(JsonValueKind.String);
        data.GetProperty("permissionCodes").GetArrayLength().ShouldBeGreaterThan(0);

        // D-SRS-01 removes the multi-assignment session surface entirely: v1 has no user
        // selectable scope, so there is no availableScopes collection and no selection state.
        data.TryGetProperty("availableScopes", out _).ShouldBeFalse(
            "availableScopes was removed by D-SRS-01; an ordinary user never selects a scope.");
        data.TryGetProperty("scopeState", out _).ShouldBeFalse(
            "SelectionRequired is not a valid v1 session state.");
    }

    [Fact]
    public async Task OpenApi_Should_DeclareActiveScopeTypeAsAStringEnum()
    {
        // Swagger is only mapped in the Development environment, so the shared Testing
        // environment returns 404. The documented contract document is what matters here.
        using HttpClient client = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development")).CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        JsonElement schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        JsonElement scopeTypeEnum = schemas.EnumerateObject()
            .First(item => item.Name.EndsWith("UserAssignmentScopeType", StringComparison.Ordinal))
            .Value;

        scopeTypeEnum.GetProperty("type").GetString().ShouldBe("string");
        scopeTypeEnum.GetProperty("enum").EnumerateArray()
            .Select(item => item.GetString())
            .ShouldBe(["Enterprise", "Site", "Warehouse"], ignoreOrder: true);
    }
}

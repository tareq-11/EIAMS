using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Domain.Common;

namespace IntegrationTests.Api;

/// <summary>
/// Locks the optimistic-concurrency contract on role metadata (D-RBAC-03 follow-up, Tranche B).
/// <para>
/// Two properties are asserted that a handler-only test cannot prove. First, the update returns
/// the complete authoritative role projection including the new <c>rowVersion</c> instead of an
/// empty body, so a client can read the version it must send next. Second,
/// <c>expectedRowVersion</c> is genuinely required: the request body binds it with
/// <c>JsonRequired</c>, so a client that omits it is rejected during model binding with
/// <c>400</c> instead of silently defaulting to zero and matching nothing.
/// </para>
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public sealed class RoleMetadataConcurrencyTests : BaseIntegrationTest
{
    private static readonly string[] EnterpriseOnly = ["Enterprise"];
    private static readonly string[] EnterpriseAndWarehouse = ["Enterprise", "Warehouse"];

    /// <summary>
    /// <c>catalog.view</c> is valid at every scope, so it is safe to grant to a role in any test
    /// here regardless of that role's allowed scope types.
    /// </summary>
    private static readonly string[] CatalogView = ["catalog.view"];

    public RoleMetadataConcurrencyTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    private sealed record RoleBody(
        Guid Id,
        string Name,
        string NameAr,
        string? Description,
        IReadOnlyCollection<string> AllowedScopeTypes,
        int RowVersion);

    [Fact]
    public async Task GetRole_Should_ExposeNameArAndRowVersion()
    {
        Guid roleId = await CreateRoleAsync();

        using HttpResponseMessage response = await HttpClient.GetAsync($"admin/roles/{roleId}");
        response.EnsureSuccessStatusCode();

        RoleBody? role = await ReadRoleAsync(response);
        role.ShouldNotBeNull();
        role.NameAr.ShouldBe("دور اختباري");
        role.RowVersion.ShouldBe(1);
        role.AllowedScopeTypes.ShouldContain("Enterprise");
    }

    [Fact]
    public async Task GetRoles_Should_ExposeNameArAndRowVersion()
    {
        await CreateRoleAsync();

        using HttpResponseMessage response = await HttpClient.GetAsync("admin/roles?page=1&pageSize=100");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Pagination is top level, so the envelope's data member is the role array itself rather
        // than an object wrapping it.
        JsonElement items = document.RootElement.GetProperty("data");
        items.ValueKind.ShouldBe(JsonValueKind.Array);

        int inspected = 0;
        foreach (JsonElement item in items.EnumerateArray())
        {
            item.GetProperty("nameAr").ValueKind.ShouldBe(JsonValueKind.String);
            item.GetProperty("rowVersion").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
            inspected++;
        }

        inspected.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task UpdateRole_Should_ReturnUpdatedProjectionWithIncrementedRowVersion()
    {
        Guid roleId = await CreateRoleAsync();

        using HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"admin/roles/{roleId}",
            new
            {
                name = "UPDATED_ROLE",
                nameAr = "دور محدَّث",
                description = "Updated by the concurrency contract test",
                expectedRowVersion = 1,
                allowedScopeTypes = EnterpriseAndWarehouse
            });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        RoleBody? updated = await ReadRoleAsync(response);
        updated.ShouldNotBeNull();
        updated.Id.ShouldBe(roleId);
        updated.Name.ShouldBe("UPDATED_ROLE");
        updated.NameAr.ShouldBe("دور محدَّث");
        updated.RowVersion.ShouldBe(2);
        updated.AllowedScopeTypes.ShouldContain("Enterprise");
        updated.AllowedScopeTypes.ShouldContain("Warehouse");
    }

    [Fact]
    public async Task UpdateRole_Should_ReturnConflict_WhenExpectedRowVersionIsStale()
    {
        Guid roleId = await CreateRoleAsync();

        using HttpResponseMessage first = await HttpClient.PutAsJsonAsync(
            $"admin/roles/{roleId}",
            new
            {
                name = "FIRST_WRITE",
                nameAr = "الكتابة الأولى",
                description = "First write wins",
                expectedRowVersion = 1,
                allowedScopeTypes = EnterpriseOnly
            });
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        // A second client replaying the version it originally read must lose, and the losing write
        // must not land.
        using HttpResponseMessage stale = await HttpClient.PutAsJsonAsync(
            $"admin/roles/{roleId}",
            new
            {
                name = "SECOND_WRITE",
                nameAr = "الكتابة الثانية",
                description = "Second write must be rejected",
                expectedRowVersion = 1,
                allowedScopeTypes = EnterpriseOnly
            });

        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = JsonDocument.Parse(await stale.Content.ReadAsStringAsync());

        // Domain.Roles.RoleErrors.RowVersionMismatch declares the dotted code; the error envelope
        // publishes it UPPER_SNAKE_CASE on the wire.
        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .ShouldBe("ROLES_ROW_VERSION_MISMATCH");

        using HttpResponseMessage reread = await HttpClient.GetAsync($"admin/roles/{roleId}");
        RoleBody? persisted = await ReadRoleAsync(reread);
        persisted.ShouldNotBeNull();
        persisted.Name.ShouldBe("FIRST_WRITE");
        persisted.RowVersion.ShouldBe(2);
    }

    [Fact]
    public async Task UpdateRole_Should_RejectRequestThatOmitsExpectedRowVersion()
    {
        Guid roleId = await CreateRoleAsync();

        using var content = new StringContent(
            $$"""
              {
                "name": "MISSING_VERSION",
                "nameAr": "بلا إصدار",
                "description": "expectedRowVersion is absent",
                "allowedScopeTypes": ["Enterprise"]
              }
              """,
            Encoding.UTF8,
            "application/json");

        using HttpResponseMessage response = await HttpClient.PutAsync($"admin/roles/{roleId}", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement error = document.RootElement.GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe("REQUEST_VALIDATION_FAILED");

        // A missing required member surfaces as a model-binding failure keyed on "body", not as a
        // per-field error, so the client is told the request was rejected without being handed a
        // field name. The security property is what matters here: omitted is rejected outright
        // rather than defaulting to zero.
        error.GetProperty("details").TryGetProperty("body", out JsonElement body).ShouldBeTrue();
        body.ValueKind.ShouldBe(JsonValueKind.Array);
        body.GetArrayLength().ShouldBeGreaterThan(0);
    }

private async Task<Guid> CreateRoleAsync()
    {
        await AuthenticateAsAdministratorAsync();

        using HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "admin/roles",
            new
            {
                name = $"ROLE_{Guid.NewGuid():N}",
                nameAr = "دور اختباري",
                description = "Concurrency contract fixture",
                permissionCodes = CatalogView,
                allowedScopeTypes = EnterpriseOnly
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    private static async Task<RoleBody?> ReadRoleAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<ApiEnvelope<RoleBody>>() is { } envelope
            ? envelope.Data
            : null;
}
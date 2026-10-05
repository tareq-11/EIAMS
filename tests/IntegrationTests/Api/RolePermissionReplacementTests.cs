using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Domain.Roles;

namespace IntegrationTests.Api;

/// <summary>
/// Locks the role permission-replacement contract (RESOLUTION-027 S16/S17/S20/S21/S23).
/// <para>
/// The load-bearing property is that role metadata and role permissions advance <em>one</em>
/// aggregate version. Before that, a permission change moved no version at all, so a client could
/// hold a version that predated someone else's grant change and save metadata over it undetected.
/// <c>MetadataUpdateAfterPermissionReplacement_Should_BeRejectedAsStale</c> is the regression guard
/// for exactly that.
/// </para>
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public sealed class RolePermissionReplacementTests : BaseIntegrationTest
{
    private static readonly string[] EnterpriseOnly = ["Enterprise"];
    private static readonly string[] EnterpriseAndWarehouse = ["Enterprise", "Warehouse"];
    private static readonly string[] CatalogView = ["catalog.view"];
    private static readonly string[] CatalogViewAndManage = ["catalog.manage", "catalog.view"];
    private static readonly string[] UnknownCode = ["catalog.view", "not.a.real.code"];

    /// <summary>
    /// <c>document.create</c> is valid at Warehouse only, so it can never take effect for a role that
    /// may only be assigned at Enterprise. Paired with the compatible <c>catalog.view</c> to prove the
    /// whole set is refused rather than the offending member being quietly dropped.
    /// </summary>
    private static readonly string[] WarehouseOnlyPlusCompatible =
        ["catalog.view", "document.create"];

    public RolePermissionReplacementTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    private sealed record RoleBody(
        Guid Id,
        string Name,
        string NameAr,
        string? Description,
        IReadOnlyCollection<string> AllowedScopeTypes,
        IReadOnlyCollection<string> PermissionCodes,
        int RowVersion);

    [Fact]
    public async Task CreateRole_Should_Return201WithTheCompleteProjection()
    {
        await AuthenticateAsAdministratorAsync();

        using HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "admin/roles",
            new
            {
                name = $"ROLE_{Guid.NewGuid():N}",
                nameAr = "دور مُنشأ",
                description = "Created with its grants",
                permissionCodes = CatalogViewAndManage,
                allowedScopeTypes = EnterpriseAndWarehouse
            });

        // S17: role creation is deliberately the one create that answers 201 with the aggregate
        // rather than a bare identifier.
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        RoleBody? created = await ReadRoleAsync(response);
        created.ShouldNotBeNull();
        created.NameAr.ShouldBe("دور مُنشأ");
        created.RowVersion.ShouldBe(1);
        created.PermissionCodes.ShouldBe(CatalogViewAndManage, ignoreOrder: true);
        created.AllowedScopeTypes.ShouldBe(EnterpriseAndWarehouse, ignoreOrder: true);
    }

    [Fact]
    public async Task CreateRole_Should_NotPersistAnything_WhenAPermissionCodeIsIncompatible()
    {
        // S16 atomicity: the grant set is validated before the role row is written, so a rejected
        // code cannot leave a role behind with no permissions.
        await AuthenticateAsAdministratorAsync();
        string name = $"ROLE_{Guid.NewGuid():N}";

        using HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "admin/roles",
            new
            {
                name,
                nameAr = "دور مرفوض",
                description = "Must not be created",
                permissionCodes = WarehouseOnlyPlusCompatible,
                allowedScopeTypes = EnterpriseOnly
            });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(response)).ShouldBe("ROLES_PERMISSION_CODES_NOT_ALLOWED_FOR_ROLE_SCOPES");

        using HttpResponseMessage listResponse = await HttpClient.GetAsync("admin/roles?page=1&pageSize=100");
        using var document = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("data").EnumerateArray()
            .ShouldNotContain(role => role.GetProperty("name").GetString() == name);
    }

    [Fact]
    public async Task CreateRole_Should_RejectAnUnknownPermissionCode()
    {
        await AuthenticateAsAdministratorAsync();

        using HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "admin/roles",
            new
            {
                name = $"ROLE_{Guid.NewGuid():N}",
                nameAr = "دور برمز خاطئ",
                description = (string?)null,
                permissionCodes = UnknownCode,
                allowedScopeTypes = EnterpriseOnly
            });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(response)).ShouldBe("ROLES_UNKNOWN_PERMISSION_CODES");
    }

    [Fact]
    public async Task ReplacePermissions_Should_ReturnUpdatedProjectionWithAdvancedVersion()
    {
        Guid roleId = await CreateRoleAsync(CatalogView, EnterpriseOnly);

        using HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"admin/roles/{roleId}/permissions",
            new { permissionCodes = CatalogViewAndManage, expectedRowVersion = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        RoleBody? updated = await ReadRoleAsync(response);
        updated.ShouldNotBeNull();
        updated.RowVersion.ShouldBe(2);
        updated.PermissionCodes.ShouldBe(CatalogViewAndManage, ignoreOrder: true);
    }

    [Fact]
    public async Task ReplacePermissions_Should_ReturnConflict_WhenExpectedRowVersionIsStale()
    {
        Guid roleId = await CreateRoleAsync(CatalogView, EnterpriseOnly);

        using HttpResponseMessage stale = await HttpClient.PutAsJsonAsync(
            $"admin/roles/{roleId}/permissions",
            new { permissionCodes = CatalogView, expectedRowVersion = 99 });

        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(stale)).ShouldBe("ROLES_ROW_VERSION_MISMATCH");

        // The rejected write must not have landed.
        using HttpResponseMessage reread = await HttpClient.GetAsync($"admin/roles/{roleId}");
        RoleBody? persisted = await ReadRoleAsync(reread);
        persisted.ShouldNotBeNull();
        persisted.PermissionCodes.ShouldBe(CatalogView);
        persisted.RowVersion.ShouldBe(1);
    }

    [Fact]
    public async Task MetadataUpdateAfterPermissionReplacement_Should_BeRejectedAsStale()
    {
        // The regression guard for S21. Permission replacement advances the same aggregate version as
        // metadata, so a client that read the role before the replacement cannot save metadata over it.
        Guid roleId = await CreateRoleAsync(CatalogView, EnterpriseOnly);

        (await HttpClient.PutAsJsonAsync(
            $"admin/roles/{roleId}/permissions",
            new { permissionCodes = CatalogViewAndManage, expectedRowVersion = 1 }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        using HttpResponseMessage metadata = await HttpClient.PutAsJsonAsync(
            $"admin/roles/{roleId}",
            new
            {
                name = "RENAMED_AFTER_PERMISSION_CHANGE",
                nameAr = "مُعاد تسميته",
                description = "Submitted against the pre-replacement version",
                expectedRowVersion = 1,
                allowedScopeTypes = EnterpriseOnly
            });

        metadata.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(metadata)).ShouldBe("ROLES_ROW_VERSION_MISMATCH");

        using HttpResponseMessage reread = await HttpClient.GetAsync($"admin/roles/{roleId}");
        RoleBody? persisted = await ReadRoleAsync(reread);
        persisted.ShouldNotBeNull();
        persisted.Name.ShouldNotBe("RENAMED_AFTER_PERMISSION_CHANGE");
    }

    [Fact]
    public async Task ReplacePermissions_Should_RejectRequestThatOmitsExpectedRowVersion()
    {
        Guid roleId = await CreateRoleAsync(CatalogView, EnterpriseOnly);

        using var content = new StringContent(
            """
            {
              "permissionCodes": ["catalog.view"]
            }
            """,
            Encoding.UTF8,
            "application/json");

        using HttpResponseMessage response = await HttpClient.PutAsync(
            $"admin/roles/{roleId}/permissions",
            content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement error = document.RootElement.GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe("REQUEST_VALIDATION_FAILED");

        // A missing required member is reported against "body", not by field name.
        error.GetProperty("details").TryGetProperty("body", out JsonElement body).ShouldBeTrue();
        body.GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ReplacePermissions_Should_RefuseTheBuiltInAdministratorRole()
    {
        await AuthenticateAsAdministratorAsync();

        using HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"admin/roles/{WellKnownRoles.AdministratorId}/permissions",
            new { permissionCodes = CatalogView, expectedRowVersion = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(response)).ShouldBe("ROLES_BUILT_IN_ROLE_IMMUTABLE");
    }

    [Fact]
    public async Task ReplacePermissions_Should_NotConsumeAVersion_WhenTheSetIsUnchanged()
    {
        // Re-submitting an unchanged set must not invalidate the caller's version, otherwise a client
        // that retries after a lost response gets a spurious 409. Matches the ExternalParty precedent.
        Guid roleId = await CreateRoleAsync(CatalogView, EnterpriseOnly);

        using HttpResponseMessage repeat = await HttpClient.PutAsJsonAsync(
            $"admin/roles/{roleId}/permissions",
            new { permissionCodes = CatalogView, expectedRowVersion = 1 });

        repeat.StatusCode.ShouldBe(HttpStatusCode.OK);
        RoleBody? unchanged = await ReadRoleAsync(repeat);
        unchanged.ShouldNotBeNull();
        unchanged.RowVersion.ShouldBe(1);
        unchanged.PermissionCodes.ShouldBe(CatalogView);
    }

    private async Task<Guid> CreateRoleAsync(IReadOnlyCollection<string> permissionCodes, string[] allowedScopeTypes)
    {
        await AuthenticateAsAdministratorAsync();

        using HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "admin/roles",
            new
            {
                name = $"ROLE_{Guid.NewGuid():N}",
                nameAr = "دور اختباري",
                description = "Concurrency contract fixture",
                permissionCodes,
                allowedScopeTypes
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("error").GetProperty("code").GetString();
    }

    private static async Task<RoleBody?> ReadRoleAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<ApiEnvelope<RoleBody>>() is { } envelope
            ? envelope.Data
            : null;
}
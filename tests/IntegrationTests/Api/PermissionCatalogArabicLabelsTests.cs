using System.Net;
using System.Text.Json;

namespace IntegrationTests.Api;

/// <summary>
/// Locks the Arabic permission catalogue required by the role permission matrix
/// (<c>docs/route-permission-scope-matrix.md</c>: the catalogue must expose <c>nameAr</c> and
/// <c>descriptionAr</c> because the permission picker renders from it).
/// <para>
/// Before this, the matrix rendered <c>permission.nameAr</c> against a catalogue that had no such
/// field, so every permission row showed an empty label with only its Latin code beneath it. These
/// tests fail if the Arabic labels are dropped from either catalogue projection.
/// </para>
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public sealed class PermissionCatalogArabicLabelsTests : BaseIntegrationTest
{
    public PermissionCatalogArabicLabelsTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    /// <summary>
    /// True when the value contains at least one Arabic-script character. Used instead of a
    /// non-empty check because the defect being guarded is precisely a label that is present but
    /// blank or non-Arabic, which would still render as an unusable row.
    /// </summary>
    private static bool ContainsArabicScript(string value)
    {
        foreach (char character in value)
        {
            if (character is >= '؀' and <= 'ۿ')
            {
                return true;
            }
        }

        return false;
    }

    [Fact]
    public async Task PermissionCatalog_Should_ServeArabicLabelsForEveryCode()
    {
        await AuthenticateAsAdministratorAsync();

        using HttpResponseMessage response = await HttpClient.GetAsync("admin/permissions?page=1&pageSize=100");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement data = document.RootElement.GetProperty("data");
        data.ValueKind.ShouldBe(JsonValueKind.Array);

        int inspected = 0;
        foreach (JsonElement item in data.EnumerateArray())
        {
            string code = item.GetProperty("code").GetString() ?? string.Empty;
            string nameAr = item.GetProperty("nameAr").GetString() ?? string.Empty;

            code.ShouldNotBeNullOrWhiteSpace();

            // A blank Arabic label is the exact defect this tranche removes: the matrix would render
            // an empty row. Require real Arabic script, not merely a non-empty string.
            nameAr.ShouldNotBeNullOrWhiteSpace();
            ContainsArabicScript(nameAr).ShouldBeTrue($"permission '{code}' must carry an Arabic nameAr");

            inspected++;
        }

        inspected.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task PermissionCatalog_Should_NotExposeLegacyColonCodes()
    {
        // The dotted cutover deleted the colon catalogue and its Down migration states the rows are
        // intentionally not recreated. A legacy code reappearing would mean the cutover was undone.
        await AuthenticateAsAdministratorAsync();

        using HttpResponseMessage response = await HttpClient.GetAsync("admin/permissions?page=1&pageSize=100");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        document.RootElement.GetProperty("data").EnumerateArray()
            .ShouldNotContain(item => (item.GetProperty("code").GetString() ?? string.Empty).Contains(':'));
    }

    [Fact]
    public async Task RolePermissionsRead_Should_ServeArabicLabelsToo()
    {
        // The role-permissions read is a second projection of the same rows; a client rendering from
        // it must not lose the labels either.
        await AuthenticateAsAdministratorAsync();

        using HttpResponseMessage response = await HttpClient.GetAsync(
            $"admin/roles/{Domain.Roles.WellKnownRoles.AuditorId}/permissions?page=1&pageSize=100");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement data = document.RootElement.GetProperty("data");
        data.ValueKind.ShouldBe(JsonValueKind.Array);
        data.GetArrayLength().ShouldBeGreaterThan(0);

        foreach (JsonElement item in data.EnumerateArray())
        {
            string code = item.GetProperty("code").GetString() ?? string.Empty;
            string nameAr = item.GetProperty("nameAr").GetString() ?? string.Empty;
            ContainsArabicScript(nameAr).ShouldBeTrue($"permission '{code}' must carry an Arabic nameAr");
        }
    }

    [Fact]
    public async Task PermissionCatalog_Should_ExposeAllowedScopeTypesForTheMatrix()
    {
        // Ruling 3 depends on the client being able to explain why a code is disabled for a role.
        await AuthenticateAsAdministratorAsync();

        using HttpResponseMessage response = await HttpClient.GetAsync("admin/permissions?page=1&pageSize=100");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        foreach (JsonElement item in document.RootElement.GetProperty("data").EnumerateArray())
        {
            JsonElement scopes = item.GetProperty("allowedScopeTypes");
            scopes.ValueKind.ShouldBe(JsonValueKind.Array);
            scopes.GetArrayLength().ShouldBeGreaterThan(0);
        }
    }
}
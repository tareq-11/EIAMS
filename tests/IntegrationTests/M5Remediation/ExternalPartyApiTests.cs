using System.Net.Http.Json;
using System.Text.Json;
using Domain.Common;
using Domain.Employees;
using Domain.Organizations;
using Domain.Permissions;
using Domain.Roles;
using Domain.OrganizationalUnits;
using Domain.Sites;
using Domain.UserRoleScopes;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.M5Remediation;

[Collection(nameof(IntegrationTestCollection))]
public sealed class ExternalPartyApiTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public ExternalPartyApiTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Admin_Should_Manage_And_Resolve_ExternalParty()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.UserRoleScopes.Where(assignment => assignment.UserId == userId).ExecuteDeleteAsync();
            var roleId = Guid.NewGuid();
            context.Roles.Add(Role.Create(roleId, $"External party admin {roleId:N}", null));
            context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(roleId, ScopeType.Enterprise));
            context.RolePermissions.AddRange(
                RolePermission.Create(roleId, WellKnownDottedPermissions.OrganizationManageId),
                RolePermission.Create(roleId, WellKnownDottedPermissions.OrganizationViewId),
                RolePermission.Create(roleId, WellKnownDottedPermissions.DocumentViewId));
            context.UserRoleScopes.Add(UserRoleScope.Create(
                Guid.NewGuid(), userId, roleId, ScopeType.Enterprise, null));
            await context.SaveChangesAsync();
        }

        Authenticate(tokens.AccessToken);

        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync(
            "external-parties",
            new
            {
                nameAr = "شركة التكامل الخارجية",
                code = "EXT-API-1",
                contactInfo = "integration@example.com",
                notes = "Integration test"
            });
        createResponse.EnsureSuccessStatusCode();

        using var createBody = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());
        Guid partyId = createBody.RootElement.GetProperty("data").GetProperty("id").GetGuid();

        HttpResponseMessage getResponse = await HttpClient.GetAsync($"external-parties/{partyId}");
        getResponse.EnsureSuccessStatusCode();
        using var getBody = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        JsonElement party = getBody.RootElement.GetProperty("data");
        party.GetProperty("nameAr").GetString().ShouldBe("شركة التكامل الخارجية");
        party.GetProperty("status").GetString().ShouldBe("Active");
        party.GetProperty("rowVersion").GetInt32().ShouldBe(1);

        HttpResponseMessage listResponse = await HttpClient.GetAsync(
            "external-parties?search=التكامل&page=1&pageSize=20");
        listResponse.EnsureSuccessStatusCode();
        string listJson = await listResponse.Content.ReadAsStringAsync();
        listJson.ShouldContain(partyId.ToString());

        HttpResponseMessage counterpartList = await HttpClient.GetAsync(
            "counterparts?operation=Receiving&type=External&search=التكامل&Page=1&PageSize=20");
        counterpartList.EnsureSuccessStatusCode();
        string counterpartListJson = await counterpartList.Content.ReadAsStringAsync();
        counterpartListJson.ShouldContain(partyId.ToString());

        HttpResponseMessage issueCounterparts = await HttpClient.GetAsync(
            "counterparts?operation=Issue&type=External&search=التكامل&page=1&pageSize=20");
        ((int)issueCounterparts.StatusCode).ShouldBeGreaterThanOrEqualTo(400);
        HttpResponseMessage issueWithoutExternalFilter = await HttpClient.GetAsync(
            "counterparts?operation=Issue&search=التكامل&page=1&pageSize=20");
        issueWithoutExternalFilter.EnsureSuccessStatusCode();
        (await issueWithoutExternalFilter.Content.ReadAsStringAsync()).ShouldNotContain(partyId.ToString());

        HttpResponseMessage resolveResponse = await HttpClient.GetAsync(
            $"counterparts/External/{partyId}");
        resolveResponse.EnsureSuccessStatusCode();

        HttpResponseMessage supplierSuggestions = await HttpClient.GetAsync(
            "receiving-infos/suppliers?search=EXT-API-1");
        supplierSuggestions.EnsureSuccessStatusCode();
        using var supplierBody = JsonDocument.Parse(
            await supplierSuggestions.Content.ReadAsStringAsync());
        JsonElement supplierResult = supplierBody.RootElement.GetProperty("data")[0];
        supplierResult.GetProperty("id").GetGuid().ShouldBe(partyId);
        supplierResult.GetProperty("code").GetString().ShouldBe("EXT-API-1");

        HttpResponseMessage updateResponse = await HttpClient.PutAsJsonAsync(
            $"external-parties/{partyId}",
            new
            {
                nameAr = "شركة التكامل المعدلة",
                code = "EXT-API-1",
                contactInfo = "updated@example.com",
                notes = "Updated",
                expectedRowVersion = 1
            });
        updateResponse.EnsureSuccessStatusCode();

        HttpResponseMessage deactivateResponse = await HttpClient.PutAsJsonAsync(
            $"external-parties/{partyId}/status",
            new { status = 1, expectedRowVersion = 2 });
        deactivateResponse.EnsureSuccessStatusCode();

        HttpResponseMessage historicalResponse = await HttpClient.GetAsync(
            $"counterparts/External/{partyId}");
        historicalResponse.EnsureSuccessStatusCode();
        string historicalJson = await historicalResponse.Content.ReadAsStringAsync();
        historicalJson.ShouldContain("Inactive");

        HttpResponseMessage activeListAfterDeactivation = await HttpClient.GetAsync(
            "counterparts?operation=Receiving&type=External&search=المعدلة&Page=1&PageSize=20");
        activeListAfterDeactivation.EnsureSuccessStatusCode();
        string activeListJson = await activeListAfterDeactivation.Content.ReadAsStringAsync();
        activeListJson.ShouldNotContain(partyId.ToString());
        HttpResponseMessage suggestionsAfterDeactivation = await HttpClient.GetAsync(
            "receiving-infos/suppliers?search=EXT-API-1");
        suggestionsAfterDeactivation.EnsureSuccessStatusCode();
        (await suggestionsAfterDeactivation.Content.ReadAsStringAsync()).ShouldNotContain(partyId.ToString());
    }

    [Fact]
    public async Task CounterpartSearch_ShouldTreatWildcardCharactersAsLiteralUnicodeInput()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.UserRoleScopes.Where(assignment => assignment.UserId == userId).ExecuteDeleteAsync();
            var roleId = Guid.NewGuid();
            context.Roles.Add(Role.Create(roleId, $"Counterpart reader {roleId:N}", null));
            context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(roleId, ScopeType.Enterprise));
            context.RolePermissions.AddRange(
                RolePermission.Create(roleId, WellKnownDottedPermissions.OrganizationManageId),
                RolePermission.Create(roleId, WellKnownDottedPermissions.DocumentViewId));
            context.UserRoleScopes.Add(UserRoleScope.Create(
                Guid.NewGuid(), userId, roleId, ScopeType.Enterprise, null));
            await context.SaveChangesAsync();
        }

        Authenticate(tokens.AccessToken);
        Guid percentId = await CreateExternalPartyAsync("شركة اختبار %", "SPECIAL-PERCENT");
        Guid underscoreId = await CreateExternalPartyAsync("شركة اختبار _", "SPECIAL-UNDERSCORE");
        Guid slashId = await CreateExternalPartyAsync(@"شركة اختبار \", "SPECIAL-SLASH");
        Guid ordinaryId = await CreateExternalPartyAsync("شركة اختبار عادي", "SPECIAL-ORDINARY");

        string percentResults = await SearchCounterpartsAsync("%25");
        percentResults.ShouldContain(percentId.ToString());
        percentResults.ShouldNotContain(underscoreId.ToString());
        percentResults.ShouldNotContain(slashId.ToString());
        percentResults.ShouldNotContain(ordinaryId.ToString());

        string underscoreResults = await SearchCounterpartsAsync("_");
        underscoreResults.ShouldContain(underscoreId.ToString());
        underscoreResults.ShouldNotContain(percentId.ToString());
        underscoreResults.ShouldNotContain(slashId.ToString());
        underscoreResults.ShouldNotContain(ordinaryId.ToString());

        string slashResults = await SearchCounterpartsAsync("%5C");
        slashResults.ShouldContain(slashId.ToString());
        slashResults.ShouldNotContain(percentId.ToString());
        slashResults.ShouldNotContain(underscoreId.ToString());
        slashResults.ShouldNotContain(ordinaryId.ToString());

        string caseInsensitiveResults = await SearchCounterpartsAsync("special-percent");
        caseInsensitiveResults.ShouldContain(percentId.ToString());
    }

    [Fact]
    public async Task CounterpartSearchAndLookup_ShouldNotRevealInternalPartiesOutsideSiteScope()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Guid foreignEmployeeId;
        string foreignEmployeeName = $"Outside employee {Guid.NewGuid():N}";
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.UserRoleScopes.Where(assignment => assignment.UserId == userId).ExecuteDeleteAsync();

            var orgId = Guid.NewGuid();
            context.Organizations.Add(Organization.Create(orgId, "Counterpart scope org", $"ORG-{Guid.NewGuid():N}"[..12]));
            var scopedSite = Site.Create(Guid.NewGuid(), orgId, "Scoped site", $"S-{Guid.NewGuid():N}"[..10], null);
            var foreignSite = Site.Create(Guid.NewGuid(), orgId, "Foreign site", $"S-{Guid.NewGuid():N}"[..10], null);
            context.Sites.AddRange(scopedSite, foreignSite);
            var foreignUnit = OrganizationalUnit.Create(Guid.NewGuid(), foreignSite.Id, null, "Foreign unit", "Department");
            var foreignEmployee = Employee.Create(Guid.NewGuid(), foreignUnit.Id, foreignEmployeeName,
                $"EMP-{Guid.NewGuid():N}"[..12], "Technician");
            context.OrganizationalUnits.Add(foreignUnit);
            context.Employees.Add(foreignEmployee);

            var roleId = Guid.NewGuid();
            context.Roles.Add(Role.Create(roleId, $"Scoped counterpart reader {roleId:N}", null));
            context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(roleId, ScopeType.Site));
            context.RolePermissions.Add(RolePermission.Create(roleId, WellKnownDottedPermissions.DocumentViewId));
            context.UserRoleScopes.Add(UserRoleScope.Create(
                Guid.NewGuid(), userId, roleId, ScopeType.Site, scopedSite.Id));
            await context.SaveChangesAsync();
            foreignEmployeeId = foreignEmployee.Id;
        }

        Authenticate(tokens.AccessToken);

        HttpResponseMessage searchResponse = await HttpClient.GetAsync(
            $"counterparts?operation=Issue&type=Employee&search={Uri.EscapeDataString(foreignEmployeeName)}&page=1&pageSize=20");
        searchResponse.EnsureSuccessStatusCode();
        string searchJson = await searchResponse.Content.ReadAsStringAsync();
        searchJson.ShouldNotContain(foreignEmployeeId.ToString());

        HttpResponseMessage inaccessible = await HttpClient.GetAsync($"counterparts/Employee/{foreignEmployeeId}");
        HttpResponseMessage nonexistent = await HttpClient.GetAsync($"counterparts/Employee/{Guid.NewGuid()}");
        inaccessible.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
        nonexistent.StatusCode.ShouldBe(inaccessible.StatusCode);
        using var inaccessibleBody = JsonDocument.Parse(await inaccessible.Content.ReadAsStringAsync());
        using var nonexistentBody = JsonDocument.Parse(await nonexistent.Content.ReadAsStringAsync());
        inaccessibleBody.RootElement.GetProperty("error").GetProperty("code").GetString()
            .ShouldBe("COUNTERPARTS_NOT_FOUND");
        nonexistentBody.RootElement.GetProperty("error").GetProperty("code").GetString()
            .ShouldBe("COUNTERPARTS_NOT_FOUND");
    }

    private async Task<Guid> CreateExternalPartyAsync(string nameAr, string code)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "external-parties",
            new { nameAr, code, contactInfo = (string?)null, notes = (string?)null });
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    private async Task<string> SearchCounterpartsAsync(string encodedSearch)
    {
        HttpResponseMessage response = await HttpClient.GetAsync(
            $"counterparts?operation=Receiving&type=External&search={encodedSearch}&page=1&pageSize=20");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}

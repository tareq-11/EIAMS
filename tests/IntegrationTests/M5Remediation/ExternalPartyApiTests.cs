using System.Net.Http.Json;
using System.Text.Json;
using Domain.Common;
using Domain.Roles;
using Domain.UserRoleScopes;
using Infrastructure.Database;
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
            context.UserRoleScopes.Add(UserRoleScope.Create(
                Guid.NewGuid(), userId, WellKnownRoles.AdministratorId, ScopeType.Enterprise, null));
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

        HttpResponseMessage counterpartList = await HttpClient.GetAsync(
            "counterparts?type=External&search=التكامل&Page=1&PageSize=20");
        counterpartList.EnsureSuccessStatusCode();
        string counterpartListJson = await counterpartList.Content.ReadAsStringAsync();
        counterpartListJson.ShouldContain(partyId.ToString());

        HttpResponseMessage resolveResponse = await HttpClient.GetAsync(
            $"counterparts/External/{partyId}");
        resolveResponse.EnsureSuccessStatusCode();

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
            "counterparts?type=External&search=المعدلة&Page=1&PageSize=20");
        activeListAfterDeactivation.EnsureSuccessStatusCode();
        string activeListJson = await activeListAfterDeactivation.Content.ReadAsStringAsync();
        activeListJson.ShouldNotContain(partyId.ToString());

        HttpResponseMessage supplierSuggestions = await HttpClient.GetAsync(
            "receiving-infos/suppliers?search=integration");
        supplierSuggestions.EnsureSuccessStatusCode();
    }
}

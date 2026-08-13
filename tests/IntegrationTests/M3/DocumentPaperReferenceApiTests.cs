using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Common;
using Domain.Organizations;
using Domain.Sites;
using Domain.UserRoleScopes;
using Domain.Warehouses;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.M3;

public sealed class DocumentPaperReferenceApiTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;
    private static readonly Guid AdministratorRoleId = new("00000000-0000-0000-0000-000000000001");

    public DocumentPaperReferenceApiTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task UpdatePaperReference_Should_ReturnUnauthorized_WhenRequestHasNoToken()
    {
        // Arrange
        Guid documentId = await SeedDraftDocumentAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"warehouse-documents/{documentId}/paper-reference",
            new { paperDocumentNumber = "P-1", paperDocumentYear = 2026, expectedRowVersion = 1 });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdatePaperReference_Should_PersistAndExposeUpdatedReference_WhenVersionMatches()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);
        Guid documentId = await SeedDraftDocumentAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"warehouse-documents/{documentId}/paper-reference",
            new { paperDocumentNumber = "P-2026-1", paperDocumentYear = 2026, expectedRowVersion = 1 });
        HttpResponseMessage getResponse = await HttpClient.GetAsync($"warehouse-documents/{documentId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument body = await ReadJsonAsync(getResponse);
        JsonElement data = body.RootElement.GetProperty("data");
        data.GetProperty("paperDocumentNumber").GetString().ShouldBe("P-2026-1");
        data.GetProperty("paperDocumentYear").GetInt32().ShouldBe(2026);
        data.GetProperty("rowVersion").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task UpdatePaperReference_Should_ReturnConflict_WhenRowVersionIsStale()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);
        Guid documentId = await SeedDraftDocumentAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"warehouse-documents/{documentId}/paper-reference",
            new { paperDocumentNumber = "P-2026-1", paperDocumentYear = 2026, expectedRowVersion = 9 });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using JsonDocument body = await ReadJsonAsync(response);
        body.RootElement.GetProperty("error").GetProperty("code").GetString()
            .ShouldBe("WAREHOUSE_DOCUMENTS_ROW_VERSION_MISMATCH");
    }

    [Fact]
    public async Task UpdatePaperReference_Should_ReturnNotFound_WhenDocumentDoesNotExist()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"warehouse-documents/{Guid.NewGuid()}/paper-reference",
            new { paperDocumentNumber = "P-2026-1", paperDocumentYear = 2026, expectedRowVersion = 1 });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<Guid> SeedDraftDocumentAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..12];
        var organization = Organization.Create(Guid.NewGuid(), $"Organization {suffix}", $"ORG{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Site {suffix}", $"S{suffix}", null);
        var warehouse = Warehouse.Create(
            Guid.NewGuid(), site.Id, $"Warehouse {suffix}", $"W{suffix}", "General", true);
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), warehouse.Id, DocumentType.Receiving, $"REC-{suffix}");
        dbContext.AddRange(organization, site, warehouse, document);
        await dbContext.SaveChangesAsync();
        return document.Id;
    }

    private async Task GrantEnterpriseAdministratorAsync(Guid userId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (await dbContext.UserRoleScopes.AnyAsync(scope =>
                scope.UserId == userId &&
                scope.RoleId == AdministratorRoleId &&
                scope.ScopeType == ScopeType.Enterprise))
        {
            return;
        }

        dbContext.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(), userId, AdministratorRoleId, ScopeType.Enterprise, null));
        await dbContext.SaveChangesAsync();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
}

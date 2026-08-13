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

namespace IntegrationTests.M4;

public sealed class ReceivingInfoApiTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;
    private static readonly Guid AdministratorRoleId = new("00000000-0000-0000-0000-000000000001");

    public ReceivingInfoApiTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task UpsertReceivingInfo_Should_PersistAndExposeDetails_WhenDocumentIsDraftReceiving()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);
        Guid documentId = await SeedDocumentAsync(DocumentType.Receiving, submitted: false);

        // Act
        HttpResponseMessage response = await PutReceivingInfoAsync(documentId, expectedRowVersion: 1);
        HttpResponseMessage getResponse = await HttpClient.GetAsync($"warehouse-documents/{documentId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument body = await ReadJsonAsync(getResponse);
        JsonElement receivingInfo = body.RootElement.GetProperty("data").GetProperty("receivingInfo");
        receivingInfo.GetProperty("supplierRef").GetString().ShouldBe("Supplier A");
        receivingInfo.GetProperty("supplierInvoiceRef").GetString().ShouldBe("INV-1");
        receivingInfo.GetProperty("receivingType").GetString().ShouldBe("Supplier");
        body.RootElement.GetProperty("data").GetProperty("rowVersion").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task UpsertReceivingInfo_Should_ReturnConflict_WhenRowVersionIsStale()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);
        Guid documentId = await SeedDocumentAsync(DocumentType.Receiving, submitted: false);

        // Act
        HttpResponseMessage response = await PutReceivingInfoAsync(documentId, expectedRowVersion: 99);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using JsonDocument body = await ReadJsonAsync(response);
        body.RootElement.GetProperty("error").GetProperty("code").GetString()
            .ShouldBe("WAREHOUSE_DOCUMENTS_ROW_VERSION_MISMATCH");
    }

    [Fact]
    public async Task UpsertReceivingInfo_Should_ReturnBadRequest_WhenDocumentIsNotReceiving()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);
        Guid documentId = await SeedDocumentAsync(DocumentType.Issue, submitted: false);

        // Act
        HttpResponseMessage response = await PutReceivingInfoAsync(documentId, expectedRowVersion: 1);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using JsonDocument body = await ReadJsonAsync(response);
        body.RootElement.GetProperty("error").GetProperty("code").GetString()
            .ShouldBe("RECEIVING_INFO_WRONG_DOCUMENT_TYPE");
    }

    [Fact]
    public async Task UpsertReceivingInfo_Should_ReturnBadRequest_WhenDocumentIsSubmitted()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);
        Guid documentId = await SeedDocumentAsync(DocumentType.Receiving, submitted: true);

        // Act
        HttpResponseMessage response = await PutReceivingInfoAsync(documentId, expectedRowVersion: 3);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using JsonDocument body = await ReadJsonAsync(response);
        body.RootElement.GetProperty("error").GetProperty("code").GetString()
            .ShouldBe("WAREHOUSE_DOCUMENTS_NOT_EDITABLE");
    }

    private async Task<HttpResponseMessage> PutReceivingInfoAsync(Guid documentId, int expectedRowVersion) =>
        await HttpClient.PutAsJsonAsync($"warehouse-documents/{documentId}/receiving-info", new
        {
            supplierRef = "Supplier A",
            supplierInvoiceRef = "INV-1",
            receivingType = "Supplier",
            expectedRowVersion
        });

    private async Task<Guid> SeedDocumentAsync(DocumentType documentType, bool submitted)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..12];
        var organization = Organization.Create(Guid.NewGuid(), $"Organization {suffix}", $"ORG{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Site {suffix}", $"S{suffix}", null);
        var warehouse = Warehouse.Create(
            Guid.NewGuid(), site.Id, $"Warehouse {suffix}", $"W{suffix}", "General", true);
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), warehouse.Id, documentType, $"DOC-{suffix}");

        if (submitted)
        {
            document.UpdatePaperReference("P-1", 2026).IsSuccess.ShouldBeTrue();
            document.Submit().IsSuccess.ShouldBeTrue();
        }

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

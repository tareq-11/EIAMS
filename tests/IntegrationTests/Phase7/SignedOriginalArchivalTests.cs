using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Domain.Common;
using Domain.DocumentAttachments;
using Domain.OrganizationalUnits;
using Domain.Organizations;
using Domain.Roles;
using Domain.Sites;
using Domain.UserRoleScopes;
using Domain.WarehouseDocuments;
using Domain.Warehouses;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Phase7;

public sealed class SignedOriginalArchivalTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public SignedOriginalArchivalTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task GetAttachments_Should_ReturnUnauthorized_WhenRequestHasNoToken()
    {
        // Arrange
        Guid documentId = await SeedDraftDocumentAsync();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            $"warehouse-documents/{documentId}/attachments");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UploadSignedOriginal_Should_ArchivePreviousVersionAndExposeHistory_WhenReplacing()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);
        Guid documentId = await SeedDraftDocumentAsync();

        Guid firstAttachmentId = await UploadSignedOriginalAsync(documentId, 1, "signed-v1.pdf", "version-one");

        // Act
        Guid secondAttachmentId = await UploadSignedOriginalAsync(documentId, 2, "signed-v2.pdf", "version-two");
        HttpResponseMessage activeResponse = await HttpClient.GetAsync(
            $"warehouse-documents/{documentId}/attachments");
        HttpResponseMessage historyResponse = await HttpClient.GetAsync(
            $"warehouse-documents/{documentId}/attachments?includeArchived=true");
        HttpResponseMessage archivedContentResponse = await HttpClient.GetAsync(
            $"warehouse-documents/{documentId}/attachments/{firstAttachmentId}/content");
        HttpResponseMessage policyResponse = await HttpClient.GetAsync(
            $"warehouse-documents/{documentId}/policy");

        // Assert
        activeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        ApiEnvelope<List<AttachmentDto>>? activeBody =
            await activeResponse.Content.ReadFromJsonAsync<ApiEnvelope<List<AttachmentDto>>>();
        activeBody.ShouldNotBeNull();
        activeBody.Data.Count.ShouldBe(1);
        activeBody.Data[0].Id.ShouldBe(secondAttachmentId);
        activeBody.Data[0].IsActive.ShouldBeTrue();

        historyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        ApiEnvelope<List<AttachmentDto>>? historyBody =
            await historyResponse.Content.ReadFromJsonAsync<ApiEnvelope<List<AttachmentDto>>>();
        historyBody.ShouldNotBeNull();
        historyBody.Data.Count.ShouldBe(2);

        AttachmentDto archived = historyBody.Data.Single(item => item.Id == firstAttachmentId);
        archived.IsActive.ShouldBeFalse();
        archived.ArchivedAtUtc.ShouldNotBeNull();
        archived.ArchivedBy.ShouldBe(userId);
        archived.ReplacedByAttachmentId.ShouldBe(secondAttachmentId);

        archivedContentResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await archivedContentResponse.Content.ReadAsStringAsync()).ShouldBe("version-one");

        policyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        ApiEnvelope<DocumentPolicyDto>? policyBody =
            await policyResponse.Content.ReadFromJsonAsync<ApiEnvelope<DocumentPolicyDto>>();
        policyBody.ShouldNotBeNull();
        policyBody.Data.SignedOriginalSatisfied.ShouldBeTrue();

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        WarehouseDocument document = await dbContext.WarehouseDocuments.SingleAsync(item => item.Id == documentId);
        document.SignedCopyAttachmentId.ShouldBe(secondAttachmentId);
        document.RowVersion.ShouldBe(3);
        (await dbContext.DocumentAttachments.CountAsync(item => item.DocumentId == documentId)).ShouldBe(2);
    }

    [Fact]
    public async Task RemoveAttachment_Should_ReturnConflict_WhenSignedOriginalIsArchived()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);
        Guid documentId = await SeedDraftDocumentAsync();
        Guid archivedAttachmentId = await UploadSignedOriginalAsync(documentId, 1, "signed-v1.pdf", "version-one");
        await UploadSignedOriginalAsync(documentId, 2, "signed-v2.pdf", "version-two");

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync(
            $"warehouse-documents/{documentId}/attachments/{archivedAttachmentId}?expectedRowVersion=3");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        ApiErrorEnvelope? body = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body.ShouldNotBeNull();
        body.Error.Code.ShouldBe("DOCUMENT_ATTACHMENTS_ARCHIVED_CANNOT_BE_REMOVED");

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await dbContext.DocumentAttachments.AnyAsync(item => item.Id == archivedAttachmentId)).ShouldBeTrue();
    }

    [Fact]
    public async Task UploadSignedOriginal_Should_ReturnBadRequest_WhenDocumentIsNotDraft()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);
        Guid documentId = await SeedDraftDocumentAsync();
        await UploadSignedOriginalAsync(documentId, 1, "signed-v1.pdf", "version-one");
        int submittedRowVersion = await SubmitDirectlyAsync(documentId);

        // Act
        HttpResponseMessage response = await SendSignedOriginalAsync(
            documentId,
            submittedRowVersion,
            "signed-v2.pdf",
            "version-two");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ApiErrorEnvelope? body = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body.ShouldNotBeNull();
        body.Error.Code.ShouldBe("DOCUMENT_ATTACHMENTS_NOT_EDITABLE");

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await dbContext.DocumentAttachments.CountAsync(item => item.DocumentId == documentId)).ShouldBe(1);
    }

    private async Task<Guid> UploadSignedOriginalAsync(
        Guid documentId,
        int expectedRowVersion,
        string filename,
        string content)
    {
        HttpResponseMessage response = await SendSignedOriginalAsync(
            documentId,
            expectedRowVersion,
            filename,
            content);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        ApiEnvelope<ResourceIdDto>? body =
            await response.Content.ReadFromJsonAsync<ApiEnvelope<ResourceIdDto>>();
        body.ShouldNotBeNull();
        return body.Data.Id;
    }

    private async Task<HttpResponseMessage> SendSignedOriginalAsync(
        Guid documentId,
        int expectedRowVersion,
        string filename,
        string content)
    {
        using var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(content));
        using var attachmentTypeContent = new StringContent(AttachmentType.SignedOriginal.ToString());
        using var rowVersionContent = new StringContent(
            expectedRowVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        using var form = new MultipartFormDataContent
        {
            { fileContent, "File", filename },
            { attachmentTypeContent, "AttachmentType" },
            { rowVersionContent, "ExpectedRowVersion" }
        };

        return await HttpClient.PostAsync(
            $"warehouse-documents/{documentId}/attachments",
            form);
    }

    private async Task<Guid> SeedDraftDocumentAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..12];
        var organization = Organization.Create(Guid.NewGuid(), $"Organization {suffix}", $"ORG{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Site {suffix}", $"S{suffix}", null);
        var organizationalUnit = OrganizationalUnit.Create(
            Guid.NewGuid(), site.Id, null, $"Directorate {suffix}", "Directorate");
        var warehouse = Warehouse.Create(
            Guid.NewGuid(),
            site.Id,
            $"Warehouse {suffix}",
            $"W{suffix}",
            "General",
            true,
            organizationalUnit.Id);
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), warehouse.Id, DocumentType.Receiving, $"REC-{suffix}");
        dbContext.AddRange(organization, site, organizationalUnit, warehouse, document);
        await dbContext.SaveChangesAsync();
        return document.Id;
    }

    private async Task<int> SubmitDirectlyAsync(Guid documentId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        WarehouseDocument document = await dbContext.WarehouseDocuments.SingleAsync(item => item.Id == documentId);
        document.UpdatePaperReference("P-2026", 2026).IsSuccess.ShouldBeTrue();
        document.Submit().IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync();
        return document.RowVersion;
    }

    private async Task GrantEnterpriseAdministratorAsync(Guid userId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        UserRoleScope? assignment = await dbContext.UserRoleScopes.SingleOrDefaultAsync(item => item.UserId == userId);

        if (assignment is not null)
        {
            assignment.RoleId.ShouldBe(WellKnownRoles.AdministratorId);
            assignment.ScopeType.ShouldBe(ScopeType.Enterprise);
            return;
        }

        dbContext.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(), userId, WellKnownRoles.AdministratorId, ScopeType.Enterprise, null));
        await dbContext.SaveChangesAsync();
    }

    private sealed record ResourceIdDto(Guid Id);

    private sealed record AttachmentDto(
        Guid Id,
        bool IsActive,
        DateTime? ArchivedAtUtc,
        Guid? ArchivedBy,
        Guid? ReplacedByAttachmentId);

    private sealed record DocumentPolicyDto(bool SignedOriginalSatisfied);

    private sealed record ApiErrorEnvelope(ApiErrorDto Error);

    private sealed record ApiErrorDto(string Code);
}

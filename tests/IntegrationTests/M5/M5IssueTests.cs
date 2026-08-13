using System.Net;
using System.Net.Http.Json;
using Application.Abstractions.Posting;
using Domain.Common;
using Domain.DocumentAttachments;
using Domain.DocumentLines;
using Domain.Employees;
using Domain.IssueTos;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.Organizations;
using Domain.Permissions;
using Domain.Roles;
using Domain.Sites;
using Domain.StockMovements;
using Domain.TransferInfos;
using Domain.UnitsOfMeasure;
using Domain.UserRoleScopes;
using Domain.WarehouseCapabilities;
using Domain.WarehouseCapabilityOperations;
using Domain.Warehouses;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.M5;

[Collection(nameof(IntegrationTestCollection))]
public sealed class M5IssueTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public M5IssueTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task UpsertIssueTo_Should_ReturnUnauthorized_WhenTokenIsMissing()
    {
        // Arrange

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"warehouse-documents/{Guid.NewGuid()}/issue-to",
            new { recipientType = "Employee", recipientId = Guid.NewGuid(), issueReason = "Need", expectedRowVersion = 1 });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task IssuePost_Should_DecrementBalanceAndWriteNegativeMovement_WhenStockIsSufficient()
    {
        // Arrange
        M5IssueSeed seed = await SeedAsync();
        await CreateAndPostOpeningAsync(seed, 10m);
        SubmittedIssue issue = await CreateSubmittedIssueAsync(seed, 4m);

        // Act
        Result<Guid> result = await PostAsync(issue.DocumentId, issue.RowVersion, seed.UserId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await GetBalanceAsync(context, seed.WarehouseId, seed.MaterialId)).ShouldBe(6m);
        StockMovement movement = await context.StockMovements.SingleAsync(item => item.DocumentId == issue.DocumentId);
        movement.MovementType.ShouldBe(MovementType.Issue);
        movement.QuantityDelta.ShouldBe(-4m);
    }

    [Fact]
    public async Task IssuePost_Should_RollBackAndRemainSubmitted_WhenStockIsInsufficient()
    {
        // Arrange
        M5IssueSeed seed = await SeedAsync();
        await CreateAndPostOpeningAsync(seed, 3m);
        SubmittedIssue issue = await CreateSubmittedIssueAsync(seed, 4m);

        // Act
        Result<Guid> result = await PostAsync(issue.DocumentId, issue.RowVersion, seed.UserId);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("InventoryBalances.InsufficientQuantity");
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await GetBalanceAsync(context, seed.WarehouseId, seed.MaterialId)).ShouldBe(3m);
        (await context.StockMovements.AnyAsync(item => item.DocumentId == issue.DocumentId)).ShouldBeFalse();
        (await context.WarehouseDocuments.SingleAsync(item => item.Id == issue.DocumentId)).DocumentStatus
            .ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public async Task UpsertIssueToAndDocumentGet_Should_ReturnDetail_WhenRecipientIsActive()
    {
        // Arrange
        M5IssueSeed seed = await SeedAsync();
        WarehouseDocument document = await CreateDraftIssueAsync(seed);
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantWarehouseDocumentPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage upsertResponse = await HttpClient.PutAsJsonAsync(
            $"warehouse-documents/{document.Id}/issue-to",
            new
            {
                recipientType = "Employee",
                recipientId = seed.EmployeeId,
                issueReason = "  Operational need  ",
                expectedRowVersion = document.RowVersion
            });
        HttpResponseMessage getResponse = await HttpClient.GetAsync($"warehouse-documents/{document.Id}");

        // Assert
        upsertResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        ApiEnvelope<DocumentDetails>? body = await getResponse.Content.ReadFromJsonAsync<ApiEnvelope<DocumentDetails>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();
        body.Data.IssueTo.ShouldNotBeNull();
        body.Data.IssueTo.RecipientId.ShouldBe(seed.EmployeeId);
        body.Data.IssueTo.RecipientType.ShouldBe("Employee");
        body.Data.IssueTo.IssueReason.ShouldBe("Operational need");
    }

    [Fact]
    public async Task UpsertIssueTo_Should_ReturnExternalNotSupportedAndNotPersist()
    {
        // Arrange
        M5IssueSeed seed = await SeedAsync();
        WarehouseDocument document = await CreateDraftIssueAsync(seed);
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantWarehouseDocumentPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"warehouse-documents/{document.Id}/issue-to",
            new
            {
                recipientType = "External",
                recipientId = Guid.NewGuid(),
                issueReason = "External request",
                expectedRowVersion = document.RowVersion
            });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ApiErrorEnvelope? body = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeFalse();
        body.Error.Code.ShouldBe("ISSUE_TOS_EXTERNAL_RECIPIENT_NOT_SUPPORTED");
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.IssueTos.AnyAsync(item => item.Id == document.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task UpsertIssueTo_Should_ReturnRecipientNotFoundAndNotPersist_WhenEmployeeDoesNotExist()
    {
        // Arrange
        M5IssueSeed seed = await SeedAsync();
        WarehouseDocument document = await CreateDraftIssueAsync(seed);
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantWarehouseDocumentPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"warehouse-documents/{document.Id}/issue-to",
            new { recipientType = "Employee", recipientId = Guid.NewGuid(), issueReason = "Need", expectedRowVersion = document.RowVersion });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ApiErrorEnvelope? body = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body.ShouldNotBeNull();
        body.Error.Code.ShouldBe("ISSUE_TOS_RECIPIENT_NOT_FOUND");
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.IssueTos.AnyAsync(item => item.Id == document.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task UpsertIssueTo_Should_ReturnRecipientInactiveAndNotPersist_WhenEmployeeIsInactive()
    {
        // Arrange
        M5IssueSeed seed = await SeedAsync();
        WarehouseDocument document = await CreateDraftIssueAsync(seed);
        await SetEmployeeInactiveAsync(seed.EmployeeId);
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantWarehouseDocumentPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"warehouse-documents/{document.Id}/issue-to",
            new { recipientType = "Employee", recipientId = seed.EmployeeId, issueReason = "Need", expectedRowVersion = document.RowVersion });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ApiErrorEnvelope? body = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body.ShouldNotBeNull();
        body.Error.Code.ShouldBe("ISSUE_TOS_RECIPIENT_INACTIVE");
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.IssueTos.AnyAsync(item => item.Id == document.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task UpsertTransferInfoAndDocumentGet_Should_ReturnDestinationDetail()
    {
        // Arrange
        M5IssueSeed seed = await SeedAsync();
        var destinationWarehouseId = Guid.NewGuid();
        WarehouseDocument document;
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Warehouse source = await context.Warehouses.SingleAsync(item => item.Id == seed.WarehouseId);
            context.Warehouses.Add(Warehouse.Create(
                destinationWarehouseId,
                source.SiteId,
                "Transfer destination",
                $"TD{Guid.NewGuid():N}"[..12],
                "Main",
                true));
            document = WarehouseDocument.CreateDraft(
                Guid.NewGuid(),
                source.Id,
                DocumentType.Transfer,
                $"TRANSFER-{Guid.NewGuid():N}");
            context.WarehouseDocuments.Add(document);
            await context.SaveChangesAsync();
        }

        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantWarehouseDocumentPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage upsertResponse = await HttpClient.PutAsJsonAsync(
            $"warehouse-documents/{document.Id}/transfer-info",
            new
            {
                destinationWarehouseId,
                transferReason = "  Site replenishment  ",
                expectedRowVersion = document.RowVersion
            });
        HttpResponseMessage getResponse = await HttpClient.GetAsync($"warehouse-documents/{document.Id}");

        // Assert
        upsertResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        ApiEnvelope<DocumentDetails>? body = await getResponse.Content.ReadFromJsonAsync<ApiEnvelope<DocumentDetails>>();
        body.ShouldNotBeNull();
        body.Data.TransferInfo.ShouldNotBeNull();
        body.Data.TransferInfo.DestinationWarehouseId.ShouldBe(destinationWarehouseId);
        body.Data.TransferInfo.TransferReason.ShouldBe("Site replenishment");
    }

    private async Task<M5IssueSeed> SeedAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var organizationalUnitId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var materialId = Guid.NewGuid();

        context.Users.Add(Domain.Users.User.Create(userId, $"m5-issue-{suffix}@example.com", "M5", "Tester", "hash"));
        context.Organizations.Add(Organization.Create(organizationId, $"Organization {suffix}", $"O{suffix}"));
        context.Sites.Add(Site.Create(siteId, organizationId, $"Site {suffix}", $"S{suffix}", null));
        context.OrganizationalUnits.Add(Domain.OrganizationalUnits.OrganizationalUnit.Create(
            organizationalUnitId, siteId, null, "Operations", "Department"));
        context.Employees.Add(Employee.Create(employeeId, organizationalUnitId, "Issue Recipient", $"E{suffix}", null));
        context.Warehouses.Add(Warehouse.Create(warehouseId, siteId, $"Warehouse {suffix}", $"W{suffix}", "Main", true));
        context.UnitsOfMeasure.Add(UnitOfMeasure.Create(unitId, $"Piece {suffix}", $"P{suffix}", "Count"));
        context.MaterialDomains.Add(MaterialDomain.Create(domainId, $"Domain {suffix}", $"D{suffix}"));
        context.MaterialCategories.Add(MaterialCategory.Create(categoryId, domainId, null, $"Category {suffix}", $"C{suffix}"));
        context.MaterialFamilies.Add(MaterialFamily.Create(familyId, categoryId, $"Family {suffix}", $"F{suffix}", unitId));
        context.Materials.Add(Material.Create(
            materialId,
            familyId,
            $"Material {suffix}",
            null,
            $"M{suffix}",
            MaterialKind.Consumable,
            TrackingType.Quantity,
            false,
            false,
            null));
        var capability = WarehouseCapability.Create(Guid.NewGuid(), warehouseId, domainId);
        context.WarehouseCapabilities.Add(capability);
        context.WarehouseCapabilityOperations.Add(WarehouseCapabilityOperation.Create(
            Guid.NewGuid(), capability.Id, OperationType.Issue));
        await context.SaveChangesAsync();

        return new M5IssueSeed(userId, warehouseId, employeeId, unitId, materialId);
    }

    private async Task<WarehouseDocument> CreateDraftIssueAsync(M5IssueSeed seed)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(),
            seed.WarehouseId,
            DocumentType.Issue,
            $"ISSUE-{Guid.NewGuid():N}");
        context.WarehouseDocuments.Add(document);
        await context.SaveChangesAsync();
        return document;
    }

    private async Task CreateAndPostOpeningAsync(M5IssueSeed seed, decimal quantity)
    {
        SubmittedIssue opening = await CreateSubmittedDocumentAsync(seed, DocumentType.Opening, quantity, OpeningType.Initial);
        (await PostAsync(opening.DocumentId, opening.RowVersion, seed.UserId)).IsSuccess.ShouldBeTrue();
    }

    private async Task SetEmployeeInactiveAsync(Guid employeeId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Employee employee = await context.Employees.SingleAsync(item => item.Id == employeeId);
        employee.SetStatus(Status.Inactive);
        await context.SaveChangesAsync();
    }

    private Task<SubmittedIssue> CreateSubmittedIssueAsync(M5IssueSeed seed, decimal quantity) =>
        CreateSubmittedDocumentAsync(seed, DocumentType.Issue, quantity, null);

    private async Task<SubmittedIssue> CreateSubmittedDocumentAsync(
        M5IssueSeed seed,
        DocumentType documentType,
        decimal quantity,
        OpeningType? openingType)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), seed.WarehouseId, documentType, $"{documentType}-{suffix}");
        Result<DocumentLine> lineResult = DocumentLine.Create(
            Guid.NewGuid(), document.Id, seed.MaterialId, DocumentLineType.Normal, quantity, seed.UnitId,
            quantity, null, null, null, openingType);
        lineResult.IsSuccess.ShouldBeTrue();
        context.WarehouseDocuments.Add(document);
        context.DocumentLines.Add(lineResult.Value);
        if (documentType == DocumentType.Issue)
        {
            Result<IssueTo> issueToResult = IssueTo.Create(document.Id, PartyType.Employee, seed.EmployeeId, "Operational need");
            issueToResult.IsSuccess.ShouldBeTrue();
            context.IssueTos.Add(issueToResult.Value);
        }

        await context.SaveChangesAsync();
        var attachment = DocumentAttachment.Create(
            Guid.NewGuid(), document.Id, AttachmentType.SignedOriginal, $"m5/{suffix}.pdf", $"{suffix}.pdf",
            "application/pdf", 1, suffix, seed.UserId, DateTime.UtcNow);
        context.DocumentAttachments.Add(attachment);
        await context.SaveChangesAsync();
        document.SetSignedCopy(attachment.Id).IsSuccess.ShouldBeTrue();
        document.UpdatePaperReference($"P-{suffix}", 2026).IsSuccess.ShouldBeTrue();
        document.Submit().IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync();

        return new SubmittedIssue(document.Id, document.RowVersion);
    }

    private async Task<Result<Guid>> PostAsync(Guid documentId, int rowVersion, Guid userId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
        return await coordinator.PostAsync(documentId, rowVersion, userId, CancellationToken.None);
    }

    private async Task GrantWarehouseDocumentPermissionsAsync(Guid userId, Guid warehouseId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleId = Guid.NewGuid();
        List<UserRoleScope> existingGrants = await context.UserRoleScopes
            .Where(scopeItem => scopeItem.UserId == userId)
            .ToListAsync();
        context.UserRoleScopes.RemoveRange(existingGrants);
        context.Roles.Add(Role.Create(roleId, $"M5 Issue {roleId:N}", null));
        context.RolePermissions.AddRange(
            RolePermission.Create(roleId, WellKnownPermissions.WarehouseDocumentsEditId),
            RolePermission.Create(roleId, WellKnownPermissions.WarehouseDocumentsViewId));
        context.UserRoleScopes.Add(UserRoleScope.Create(Guid.NewGuid(), userId, roleId, ScopeType.Warehouse, warehouseId));
        await context.SaveChangesAsync();
    }

    private static Task<decimal> GetBalanceAsync(ApplicationDbContext context, Guid warehouseId, Guid materialId) =>
        context.InventoryBalances
            .Where(balance => balance.WarehouseId == warehouseId && balance.MaterialId == materialId)
            .Select(balance => balance.Quantity)
            .SingleAsync();

    private sealed record M5IssueSeed(Guid UserId, Guid WarehouseId, Guid EmployeeId, Guid UnitId, Guid MaterialId);
    private sealed record SubmittedIssue(Guid DocumentId, int RowVersion);
    private sealed record DocumentDetails(IssueToDetails? IssueTo, TransferInfoDetails? TransferInfo);
    private sealed record IssueToDetails(string RecipientType, Guid RecipientId, string IssueReason);
    private sealed record TransferInfoDetails(Guid DestinationWarehouseId, string TransferReason);
    private sealed record ApiErrorEnvelope(bool Success, ApiErrorDetails Error);
    private sealed record ApiErrorDetails(string Code);
}

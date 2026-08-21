using System.Net;
using System.Net.Http.Json;
using Application.Abstractions.Posting;
using Domain.AssetMovementHistories;
using Domain.Assets;
using Domain.Common;
using Domain.Custodies;
using Domain.CustodyHistories;
using Domain.DocumentAttachments;
using Domain.DocumentLineAssetSelections;
using Domain.DocumentLines;
using Domain.Employees;
using Domain.IssueTos;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.Organizations;
using Domain.OrganizationalUnits;
using Domain.Permissions;
using Domain.ReceivingInfos;
using Domain.ReturnInfos;
using Domain.Roles;
using Domain.Sites;
using Domain.UnitsOfMeasure;
using Domain.UserRoleScopes;
using Domain.Users;
using Domain.WarehouseCapabilities;
using Domain.WarehouseCapabilityOperations;
using Domain.Warehouses;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.M6;

[Collection(nameof(IntegrationTestCollection))]
public sealed class M6AssetLifecyclePostingTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public M6AssetLifecyclePostingTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task ReceivingPost_Should_CreateReceivedHistoryAndDeriveInStock()
    {
        // Arrange
        M6Seed seed = await SeedAsync();
        SubmittedDocument receiving = await CreateSubmittedReceivingAsync(seed, 1m, 2m);

        // Act
        Result<Guid> result = await PostAsync(receiving.Id, receiving.RowVersion, seed.PostedBy);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Asset asset = await context.Assets.SingleAsync(item => item.ReceiptLineId == receiving.AssetLineId);
        AssetMovementHistory history = await context.AssetMovementHistories.SingleAsync(item => item.AssetId == asset.Id);
        history.DocumentId.ShouldBe(receiving.Id);
        history.MovementType.ShouldBe(AssetMovementType.Received);
        AssetCurrentStatusView status = await context.AssetCurrentStatuses.SingleAsync(item => item.AssetId == asset.Id);
        status.CurrentStatus.ShouldBe(AssetCurrentStatus.InStock);
        status.ActiveCustodyId.ShouldBeNull();
    }

    [Fact]
    public async Task AssetIssuePost_Should_WriteLedgerHistoryCustodyAndDerivedStatus_WhenExactSelectionExists()
    {
        // Arrange
        M6Seed seed = await SeedAsync();
        SubmittedDocument receiving = await CreateSubmittedReceivingAsync(seed, 1m, 2m);
        (await PostAsync(receiving.Id, receiving.RowVersion, seed.PostedBy)).IsSuccess.ShouldBeTrue();
        Guid assetId = await GetAssetIdAsync(receiving.AssetLineId);
        SubmittedDocument issue = await CreateSubmittedIssueAsync(
            seed,
            PartyType.OrganizationalUnit,
            seed.OrganizationalUnitId,
            [assetId],
            assetQuantity: 1m,
            normalQuantity: 1m);

        // Act
        Result<Guid> result = await PostAsync(issue.Id, issue.RowVersion, seed.PostedBy);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await GetBalanceAsync(context, seed.WarehouseId, seed.AssetMaterialId)).ShouldBe(0m);
        (await GetBalanceAsync(context, seed.WarehouseId, seed.NormalMaterialId)).ShouldBe(1m);
        (await context.StockMovements.Where(item => item.DocumentId == issue.Id).CountAsync()).ShouldBe(2);
        AssetMovementHistory history = await context.AssetMovementHistories
            .SingleAsync(item => item.AssetId == assetId && item.DocumentId == issue.Id);
        history.MovementType.ShouldBe(AssetMovementType.Issued);
        Custody custody = await context.Custodies.SingleAsync(item => item.AssetId == assetId);
        custody.Status.ShouldBe(CustodyStatus.Active);
        custody.CustodyKind.ShouldBe(CustodyKind.Operational);
        custody.HolderId.ShouldBe(seed.OrganizationalUnitId);
        AssetCurrentStatusView status = await context.AssetCurrentStatuses.SingleAsync(item => item.AssetId == assetId);
        status.CurrentStatus.ShouldBe(AssetCurrentStatus.Issued);
        status.ActiveCustodyId.ShouldBe(custody.Id);
    }

    [Fact]
    public async Task AssetIssuePost_Should_RollBack_WhenSelectionCountMismatches()
    {
        // Arrange
        M6Seed seed = await SeedAsync();
        SubmittedDocument receiving = await CreateSubmittedReceivingAsync(seed, 2m, 0m);
        (await PostAsync(receiving.Id, receiving.RowVersion, seed.PostedBy)).IsSuccess.ShouldBeTrue();
        Guid selectedAssetId = await GetAssetIdAsync(receiving.AssetLineId);
        SubmittedDocument issue = await CreateSubmittedIssueAsync(
            seed,
            PartyType.OrganizationalUnit,
            seed.OrganizationalUnitId,
            [selectedAssetId],
            assetQuantity: 2m,
            normalQuantity: 0m);

        // Act
        Result<Guid> result = await PostAsync(issue.Id, issue.RowVersion, seed.PostedBy);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("DocumentLineAssetSelections.CountMismatch");
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.StockMovements.AnyAsync(item => item.DocumentId == issue.Id)).ShouldBeFalse();
        (await context.AssetMovementHistories.AnyAsync(item => item.DocumentId == issue.Id)).ShouldBeFalse();
        (await context.Custodies.AnyAsync(item => item.IssueDocumentId == issue.Id)).ShouldBeFalse();
        (await context.WarehouseDocuments.SingleAsync(item => item.Id == issue.Id)).DocumentStatus
            .ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public async Task AssetIssuePost_Should_RollBackAssetEffects_WhenConsumableStockIsInsufficient()
    {
        // Arrange
        M6Seed seed = await SeedAsync();
        SubmittedDocument receiving = await CreateSubmittedReceivingAsync(seed, 1m, 1m);
        (await PostAsync(receiving.Id, receiving.RowVersion, seed.PostedBy)).IsSuccess.ShouldBeTrue();
        Guid assetId = await GetAssetIdAsync(receiving.AssetLineId);
        SubmittedDocument issue = await CreateSubmittedIssueAsync(
            seed,
            PartyType.OrganizationalUnit,
            seed.OrganizationalUnitId,
            [assetId],
            assetQuantity: 1m,
            normalQuantity: 2m);

        // Act
        Result<Guid> result = await PostAsync(issue.Id, issue.RowVersion, seed.PostedBy);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("InventoryBalances.InsufficientQuantity");
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await GetBalanceAsync(context, seed.WarehouseId, seed.AssetMaterialId)).ShouldBe(1m);
        (await GetBalanceAsync(context, seed.WarehouseId, seed.NormalMaterialId)).ShouldBe(1m);
        (await context.StockMovements.AnyAsync(item => item.DocumentId == issue.Id)).ShouldBeFalse();
        (await context.AssetMovementHistories.AnyAsync(item => item.DocumentId == issue.Id)).ShouldBeFalse();
        (await context.Custodies.AnyAsync(item => item.IssueDocumentId == issue.Id)).ShouldBeFalse();
        (await context.AssetCurrentStatuses.SingleAsync(item => item.AssetId == assetId)).CurrentStatus
            .ShouldBe(AssetCurrentStatus.InStock);
    }

    [Fact]
    public async Task ConcurrentAssetIssues_Should_AllowOnlyOnePost_WhenBothSelectTheSameAsset()
    {
        // Arrange
        M6Seed seed = await SeedAsync();
        SubmittedDocument receiving = await CreateSubmittedReceivingAsync(seed, 1m, 0m);
        (await PostAsync(receiving.Id, receiving.RowVersion, seed.PostedBy)).IsSuccess.ShouldBeTrue();
        Guid assetId = await GetAssetIdAsync(receiving.AssetLineId);
        SubmittedDocument first = await CreateSubmittedIssueAsync(
            seed,
            PartyType.OrganizationalUnit,
            seed.OrganizationalUnitId,
            [assetId],
            assetQuantity: 1m,
            normalQuantity: 0m);
        SubmittedDocument second = await CreateSubmittedIssueAsync(
            seed,
            PartyType.OrganizationalUnit,
            seed.OrganizationalUnitId,
            [assetId],
            assetQuantity: 1m,
            normalQuantity: 0m);

        // Act
        Result<Guid>[] results = await Task.WhenAll(
            PostAsync(first.Id, first.RowVersion, seed.PostedBy),
            PostAsync(second.Id, second.RowVersion, seed.PostedBy));

        // Assert
        results.Count(result => result.IsSuccess).ShouldBe(1);
        Result<Guid> failure = results.Single(result => result.IsFailure);
        failure.Error.Code.ShouldBe("DocumentLineAssetSelections.AssetNotInStock");
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.StockMovements.CountAsync(item => item.MovementType == MovementType.Issue &&
            (item.DocumentId == first.Id || item.DocumentId == second.Id))).ShouldBe(1);
        (await context.AssetMovementHistories.CountAsync(item => item.AssetId == assetId &&
            item.MovementType == AssetMovementType.Issued)).ShouldBe(1);
        (await context.Custodies.CountAsync(item => item.AssetId == assetId && item.Status == CustodyStatus.Active)).ShouldBe(1);
        (await GetBalanceAsync(context, seed.WarehouseId, seed.AssetMaterialId)).ShouldBe(0m);
    }

    [Fact]
    public async Task CustodyAssignment_Should_CloseOperationalCustodyAndOpenPersonalCustody()
    {
        // Arrange
        M6Seed seed = await SeedAsync();
        SubmittedDocument receiving = await CreateSubmittedReceivingAsync(seed, 1m, 0m);
        (await PostAsync(receiving.Id, receiving.RowVersion, seed.PostedBy)).IsSuccess.ShouldBeTrue();
        Guid assetId = await GetAssetIdAsync(receiving.AssetLineId);
        SubmittedDocument issue = await CreateSubmittedIssueAsync(
            seed,
            PartyType.OrganizationalUnit,
            seed.OrganizationalUnitId,
            [assetId],
            assetQuantity: 1m,
            normalQuantity: 0m);
        (await PostAsync(issue.Id, issue.RowVersion, seed.PostedBy)).IsSuccess.ShouldBeTrue();
        Custody operational = await GetActiveCustodyAsync(assetId);
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEditPermissionAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            $"assets/{assetId}/custody-assignment",
            new { employeeId = seed.EmployeeId, expectedCustodyRowVersion = operational.RowVersion, note = "Assigned" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Custody closed = await context.Custodies.SingleAsync(item => item.Id == operational.Id);
        closed.Status.ShouldBe(CustodyStatus.Closed);
        Custody personal = await context.Custodies.SingleAsync(item => item.AssetId == assetId && item.Status == CustodyStatus.Active);
        personal.CustodyKind.ShouldBe(CustodyKind.Personal);
        personal.HolderType.ShouldBe(PartyType.Employee);
        personal.HolderId.ShouldBe(seed.EmployeeId);
        (await context.CustodyHistories.AnyAsync(item => item.CustodyId == operational.Id &&
            item.FromStatus == CustodyStatus.Active && item.ToStatus == CustodyStatus.Closed)).ShouldBeTrue();
        (await context.AssetCurrentStatuses.SingleAsync(item => item.AssetId == assetId)).CurrentStatus
            .ShouldBe(AssetCurrentStatus.InCustody);
    }

    [Fact]
    public async Task ReturnPost_Should_RestoreAssetAndConsumableAndCloseCustody()
    {
        // Arrange
        M6Seed seed = await SeedAsync();
        SubmittedDocument receiving = await CreateSubmittedReceivingAsync(seed, 1m, 5m);
        (await PostAsync(receiving.Id, receiving.RowVersion, seed.PostedBy)).IsSuccess.ShouldBeTrue();
        Guid assetId = await GetAssetIdAsync(receiving.AssetLineId);
        SubmittedDocument issue = await CreateSubmittedIssueAsync(
            seed,
            PartyType.OrganizationalUnit,
            seed.OrganizationalUnitId,
            [assetId],
            assetQuantity: 1m,
            normalQuantity: 2m);
        (await PostAsync(issue.Id, issue.RowVersion, seed.PostedBy)).IsSuccess.ShouldBeTrue();
        SubmittedDocument returned = await CreateSubmittedReturnAsync(seed, issue.Id, [assetId], 1m, 1m);

        // Act
        Result<Guid> result = await PostAsync(returned.Id, returned.RowVersion, seed.PostedBy);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await GetBalanceAsync(context, seed.WarehouseId, seed.AssetMaterialId)).ShouldBe(1m);
        (await GetBalanceAsync(context, seed.WarehouseId, seed.NormalMaterialId)).ShouldBe(4m);
        Custody custody = await context.Custodies.SingleAsync(item => item.AssetId == assetId);
        custody.Status.ShouldBe(CustodyStatus.Closed);
        custody.ReturnDocumentId.ShouldBe(returned.Id);
        (await context.AssetMovementHistories.SingleAsync(item => item.AssetId == assetId && item.DocumentId == returned.Id))
            .MovementType.ShouldBe(AssetMovementType.Returned);
        (await context.AssetCurrentStatuses.SingleAsync(item => item.AssetId == assetId)).CurrentStatus
            .ShouldBe(AssetCurrentStatus.InStock);
    }

    [Fact]
    public async Task IssueReversal_Should_Reject_WhenCustodyWasReassigned()
    {
        // Arrange
        M6Seed seed = await SeedAsync();
        SubmittedDocument receiving = await CreateSubmittedReceivingAsync(seed, 1m, 0m);
        (await PostAsync(receiving.Id, receiving.RowVersion, seed.PostedBy)).IsSuccess.ShouldBeTrue();
        Guid assetId = await GetAssetIdAsync(receiving.AssetLineId);
        SubmittedDocument issue = await CreateSubmittedIssueAsync(
            seed,
            PartyType.OrganizationalUnit,
            seed.OrganizationalUnitId,
            [assetId],
            assetQuantity: 1m,
            normalQuantity: 0m);
        (await PostAsync(issue.Id, issue.RowVersion, seed.PostedBy)).IsSuccess.ShouldBeTrue();
        await AssignCustodyDirectlyAsync(assetId, seed.EmployeeId);
        SubmittedDocument reversal = await CreateSubmittedReversalAsync(issue.Id);

        // Act
        Result<Guid> result = await PostAsync(reversal.Id, reversal.RowVersion, seed.PostedBy);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Custodies.CannotReverseChangedCustody");
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.StockMovements.AnyAsync(item => item.DocumentId == reversal.Id)).ShouldBeFalse();
        (await context.WarehouseDocuments.SingleAsync(item => item.Id == issue.Id)).DocumentStatus
            .ShouldBe(DocumentStatus.Posted);
    }

    [Fact]
    public async Task ReturnReversal_Should_ReopenCustodyAndRestoreIssuedState()
    {
        // Arrange
        M6Seed seed = await SeedAsync();
        SubmittedDocument receiving = await CreateSubmittedReceivingAsync(seed, 1m, 1m);
        (await PostAsync(receiving.Id, receiving.RowVersion, seed.PostedBy)).IsSuccess.ShouldBeTrue();
        Guid assetId = await GetAssetIdAsync(receiving.AssetLineId);
        SubmittedDocument issue = await CreateSubmittedIssueAsync(
            seed,
            PartyType.OrganizationalUnit,
            seed.OrganizationalUnitId,
            [assetId],
            assetQuantity: 1m,
            normalQuantity: 1m);
        (await PostAsync(issue.Id, issue.RowVersion, seed.PostedBy)).IsSuccess.ShouldBeTrue();
        SubmittedDocument returned = await CreateSubmittedReturnAsync(seed, issue.Id, [assetId], 1m, 1m);
        (await PostAsync(returned.Id, returned.RowVersion, seed.PostedBy)).IsSuccess.ShouldBeTrue();
        SubmittedDocument reversal = await CreateSubmittedReversalAsync(returned.Id);

        // Act
        Result<Guid> result = await PostAsync(reversal.Id, reversal.RowVersion, seed.PostedBy);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Custody custody = await context.Custodies.SingleAsync(item => item.AssetId == assetId);
        custody.Status.ShouldBe(CustodyStatus.Active);
        custody.ReturnDocumentId.ShouldBeNull();
        (await GetBalanceAsync(context, seed.WarehouseId, seed.AssetMaterialId)).ShouldBe(0m);
        (await GetBalanceAsync(context, seed.WarehouseId, seed.NormalMaterialId)).ShouldBe(0m);
        (await context.AssetCurrentStatuses.SingleAsync(item => item.AssetId == assetId)).CurrentStatus
            .ShouldBe(AssetCurrentStatus.Issued);
    }

    private async Task<M6Seed> SeedAsync()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var postedBy = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var organizationalUnitId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var assetMaterialId = Guid.NewGuid();
        var normalMaterialId = Guid.NewGuid();

        context.Users.Add(User.Create(postedBy, $"m6-{suffix}@example.com", "M6", "Poster", "hash"));
        context.Organizations.Add(Organization.Create(organizationId, $"Organization {suffix}", $"O{suffix}"));
        context.Sites.Add(Site.Create(siteId, organizationId, $"Site {suffix}", $"S{suffix}", null));
        context.OrganizationalUnits.Add(OrganizationalUnit.Create(
            organizationalUnitId, siteId, null, $"Operations {suffix}", "Department"));
        context.Employees.Add(Employee.Create(employeeId, organizationalUnitId, $"Employee {suffix}", $"E{suffix}", null));
        context.Warehouses.Add(Warehouse.Create(warehouseId, siteId, $"Warehouse {suffix}", $"W{suffix}", "Main", true));
        context.UnitsOfMeasure.Add(UnitOfMeasure.Create(unitId, $"Piece {suffix}", $"P{suffix}", "Count"));
        context.MaterialDomains.Add(MaterialDomain.Create(domainId, $"Domain {suffix}", $"D{suffix}"));
        context.MaterialCategories.Add(MaterialCategory.Create(categoryId, domainId, null, $"Category {suffix}", $"C{suffix}"));
        context.MaterialFamilies.Add(MaterialFamily.Create(familyId, categoryId, $"Family {suffix}", $"F{suffix}", unitId));
        context.Materials.Add(Material.Create(
            assetMaterialId, familyId, $"Asset {suffix}", null, $"A{suffix}", MaterialKind.Asset,
            TrackingType.Serial, false, true, null));
        context.Materials.Add(Material.Create(
            normalMaterialId, familyId, $"Consumable {suffix}", null, $"M{suffix}", MaterialKind.Consumable,
            TrackingType.Quantity, false, false, null));
        var capability = WarehouseCapability.Create(Guid.NewGuid(), warehouseId, domainId);
        context.WarehouseCapabilities.Add(capability);
        context.WarehouseCapabilityOperations.AddRange(
            WarehouseCapabilityOperation.Create(Guid.NewGuid(), capability.Id, OperationType.Receiving),
            WarehouseCapabilityOperation.Create(Guid.NewGuid(), capability.Id, OperationType.Issue),
            WarehouseCapabilityOperation.Create(Guid.NewGuid(), capability.Id, OperationType.Return));
        await context.SaveChangesAsync();

        return new M6Seed(
            postedBy, warehouseId, organizationalUnitId, employeeId, unitId, assetMaterialId, normalMaterialId);
    }

    private async Task<SubmittedDocument> CreateSubmittedReceivingAsync(M6Seed seed, decimal assetQuantity, decimal normalQuantity)
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), seed.WarehouseId, DocumentType.Receiving, $"REC-{suffix}");
        Result<DocumentLine> assetLine = DocumentLine.Create(
            Guid.NewGuid(), document.Id, seed.AssetMaterialId, DocumentLineType.Asset, assetQuantity, seed.UnitId,
            assetQuantity, null, null, null);
        assetLine.IsSuccess.ShouldBeTrue();
        context.WarehouseDocuments.Add(document);
        context.DocumentLines.Add(assetLine.Value);
        Guid? normalLineId = null;
        if (normalQuantity > 0)
        {
            Result<DocumentLine> normalLine = DocumentLine.Create(
                Guid.NewGuid(), document.Id, seed.NormalMaterialId, DocumentLineType.Normal, normalQuantity, seed.UnitId,
                normalQuantity, null, null, null);
            normalLine.IsSuccess.ShouldBeTrue();
            context.DocumentLines.Add(normalLine.Value);
            normalLineId = normalLine.Value.Id;
        }

        Result<ReceivingInfo> receivingInfo = ReceivingInfo.Create(
            document.Id, $"Supplier {suffix}", null, ReceivingType.Supplier);
        receivingInfo.IsSuccess.ShouldBeTrue();
        context.ReceivingInfos.Add(receivingInfo.Value);
        await context.SaveChangesAsync();
        await AddSignedOriginalAndSubmitAsync(context, document, seed.PostedBy, suffix);

        return new SubmittedDocument(document.Id, document.RowVersion, assetLine.Value.Id, normalLineId);
    }

    private async Task<SubmittedDocument> CreateSubmittedIssueAsync(
        M6Seed seed,
        PartyType recipientType,
        Guid recipientId,
        IReadOnlyCollection<Guid> assetIds,
        decimal assetQuantity,
        decimal normalQuantity)
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), seed.WarehouseId, DocumentType.Issue, $"ISS-{suffix}");
        Result<DocumentLine> assetLine = DocumentLine.Create(
            Guid.NewGuid(), document.Id, seed.AssetMaterialId, DocumentLineType.Asset, assetQuantity, seed.UnitId,
            assetQuantity, null, null, null);
        assetLine.IsSuccess.ShouldBeTrue();
        context.WarehouseDocuments.Add(document);
        context.DocumentLines.Add(assetLine.Value);
        if (normalQuantity > 0)
        {
            Result<DocumentLine> normalLine = DocumentLine.Create(
                Guid.NewGuid(), document.Id, seed.NormalMaterialId, DocumentLineType.Normal, normalQuantity, seed.UnitId,
                normalQuantity, null, null, null);
            normalLine.IsSuccess.ShouldBeTrue();
            context.DocumentLines.Add(normalLine.Value);
        }

        Result<IssueTo> issueTo = IssueTo.Create(document.Id, recipientType, recipientId, "Operational need");
        issueTo.IsSuccess.ShouldBeTrue();
        context.IssueTos.Add(issueTo.Value);
        foreach (Guid assetId in assetIds)
        {
            Result<DocumentLineAssetSelection> selection = DocumentLineAssetSelection.Create(
                Guid.NewGuid(), document.Id, assetLine.Value.Id, assetId);
            selection.IsSuccess.ShouldBeTrue();
            context.DocumentLineAssetSelections.Add(selection.Value);
        }

        await context.SaveChangesAsync();
        await AddSignedOriginalAndSubmitAsync(context, document, seed.PostedBy, suffix);
        return new SubmittedDocument(document.Id, document.RowVersion, assetLine.Value.Id, null);
    }

    private async Task<SubmittedDocument> CreateSubmittedReturnAsync(
        M6Seed seed,
        Guid originalIssueId,
        IReadOnlyCollection<Guid> assetIds,
        decimal assetQuantity,
        decimal normalQuantity)
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), seed.WarehouseId, DocumentType.Return, $"RET-{suffix}");
        Result<DocumentLine> assetLine = DocumentLine.Create(
            Guid.NewGuid(), document.Id, seed.AssetMaterialId, DocumentLineType.Asset, assetQuantity, seed.UnitId,
            assetQuantity, null, null, null);
        assetLine.IsSuccess.ShouldBeTrue();
        context.WarehouseDocuments.Add(document);
        context.DocumentLines.Add(assetLine.Value);
        if (normalQuantity > 0)
        {
            Result<DocumentLine> normalLine = DocumentLine.Create(
                Guid.NewGuid(), document.Id, seed.NormalMaterialId, DocumentLineType.Normal, normalQuantity, seed.UnitId,
                normalQuantity, null, null, null);
            normalLine.IsSuccess.ShouldBeTrue();
            context.DocumentLines.Add(normalLine.Value);
        }

        Result<ReturnInfo> returnInfo = ReturnInfo.Create(document.Id, originalIssueId, "Returned in good condition");
        returnInfo.IsSuccess.ShouldBeTrue();
        context.ReturnInfos.Add(returnInfo.Value);
        foreach (Guid assetId in assetIds)
        {
            Result<DocumentLineAssetSelection> selection = DocumentLineAssetSelection.Create(
                Guid.NewGuid(), document.Id, assetLine.Value.Id, assetId);
            selection.IsSuccess.ShouldBeTrue();
            context.DocumentLineAssetSelections.Add(selection.Value);
        }

        await context.SaveChangesAsync();
        await AddSignedOriginalAndSubmitAsync(context, document, seed.PostedBy, suffix);
        return new SubmittedDocument(document.Id, document.RowVersion, assetLine.Value.Id, null);
    }

    private async Task<SubmittedDocument> CreateSubmittedReversalAsync(Guid sourceDocumentId)
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        WarehouseDocument source = await context.WarehouseDocuments.SingleAsync(item => item.Id == sourceDocumentId);
        List<DocumentLine> sourceLines = await context.DocumentLines
            .Where(item => item.DocumentId == sourceDocumentId)
            .OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.Id)
            .ToListAsync();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var reversal = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), source.WarehouseId, source.DocumentType, $"REV-{suffix}", source.Id);
        context.WarehouseDocuments.Add(reversal);
        foreach (DocumentLine sourceLine in sourceLines)
        {
            Result<DocumentLine> reversalLine = DocumentLine.Create(
                Guid.NewGuid(), reversal.Id, sourceLine.MaterialId, sourceLine.LineType, sourceLine.Quantity,
                sourceLine.UnitId, sourceLine.BaseQuantity, sourceLine.UnitPrice, sourceLine.BatchNumber,
                sourceLine.ExpiryDate, sourceLine.OpeningType, sourceLine.Id);
            reversalLine.IsSuccess.ShouldBeTrue();
            context.DocumentLines.Add(reversalLine.Value);
        }

        await context.SaveChangesAsync();
        Guid postedBy = await context.WarehouseDocuments.Where(item => item.Id == sourceDocumentId)
            .Select(item => item.PostedBy!.Value).SingleAsync();
        await AddSignedOriginalAndSubmitAsync(context, reversal, postedBy, suffix);
        return new SubmittedDocument(reversal.Id, reversal.RowVersion, Guid.Empty, null);
    }

    private static async Task AddSignedOriginalAndSubmitAsync(
        ApplicationDbContext context,
        WarehouseDocument document,
        Guid userId,
        string suffix)
    {
        // Arrange
        var attachment = DocumentAttachment.Create(
            Guid.NewGuid(), document.Id, AttachmentType.SignedOriginal, $"m6/{suffix}.pdf", $"{suffix}.pdf",
            "application/pdf", 1, suffix, userId, DateTime.UtcNow);
        context.DocumentAttachments.Add(attachment);
        await context.SaveChangesAsync();
        document.SetSignedCopy(attachment.Id).IsSuccess.ShouldBeTrue();
        document.UpdatePaperReference($"P-{suffix}", 2026).IsSuccess.ShouldBeTrue();
        document.Submit().IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync();
    }

    private async Task AssignCustodyDirectlyAsync(Guid assetId, Guid employeeId)
    {
        // Arrange
        Custody custody = await GetActiveCustodyAsync(assetId);
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Guid warehouseId = await GetWarehouseIdForCustodyAsync(custody.Id);
        await GrantEditPermissionAsync(userId, warehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            $"assets/{assetId}/custody-assignment",
            new { employeeId, expectedCustodyRowVersion = custody.RowVersion, note = "Assigned" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task<Result<Guid>> PostAsync(Guid documentId, int rowVersion, Guid postedBy)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
        Result<PostingOutcome> result = await coordinator.PostAsync(
            documentId, rowVersion, postedBy, CancellationToken.None);
        return result.IsFailure
            ? Result.Failure<Guid>(result.Error)
            : result.Value.DocumentId;
    }

    private async Task<Guid> GetAssetIdAsync(Guid receiptLineId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Assets.Where(item => item.ReceiptLineId == receiptLineId).Select(item => item.Id).FirstAsync();
    }

    private async Task<Custody> GetActiveCustodyAsync(Guid assetId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Custodies.SingleAsync(item => item.AssetId == assetId && item.Status == CustodyStatus.Active);
    }

    private async Task<Guid> GetWarehouseIdForCustodyAsync(Guid custodyId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await (
            from custody in context.Custodies
            join issue in context.WarehouseDocuments on custody.IssueDocumentId equals issue.Id
            where custody.Id == custodyId
            select issue.WarehouseId).SingleAsync();
    }

    private async Task GrantEditPermissionAsync(Guid userId, Guid warehouseId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleId = Guid.NewGuid();
        context.Roles.Add(Role.Create(roleId, $"M6 edit {roleId:N}", null));
        context.RolePermissions.Add(RolePermission.Create(roleId, WellKnownPermissions.WarehouseDocumentsEditId));
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(), userId, roleId, ScopeType.Warehouse, warehouseId));
        await context.SaveChangesAsync();
    }

    private static Task<decimal> GetBalanceAsync(ApplicationDbContext context, Guid warehouseId, Guid materialId) =>
        context.InventoryBalances.Where(item => item.WarehouseId == warehouseId && item.MaterialId == materialId)
            .Select(item => item.Quantity).SingleAsync();

    private sealed record M6Seed(
        Guid PostedBy,
        Guid WarehouseId,
        Guid OrganizationalUnitId,
        Guid EmployeeId,
        Guid UnitId,
        Guid AssetMaterialId,
        Guid NormalMaterialId);

    private sealed record SubmittedDocument(Guid Id, int RowVersion, Guid AssetLineId, Guid? NormalLineId);
}

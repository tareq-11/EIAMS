using System.Net;
using System.Net.Http.Json;
using Application.Abstractions.Posting;
using Domain.AssetMovementHistories;
using Domain.Assets;
using Domain.Common;
using Domain.Custodies;
using Domain.DocumentAttachments;
using Domain.DocumentLineAssetSelections;
using Domain.DocumentLines;
using Domain.InventoryAdjustments;
using Domain.InventoryCounts;
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
using Domain.UnitsOfMeasure;
using Domain.UserRoleScopes;
using Domain.Users;
using Domain.WarehouseCapabilities;
using Domain.WarehouseCapabilityOperations;
using Domain.WarehouseDocuments;
using Domain.Warehouses;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.M7;

[Collection(nameof(IntegrationTestCollection))]
public sealed class M7AdjustmentAndFreezeTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public M7AdjustmentAndFreezeTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task QuantityAdjustment_Should_PostSignedDifference_AndUpdateBalance()
    {
        // Arrange
        M7Seed seed = await SeedAsync();
        await CreateAndPostOpeningAsync(seed, 5m);
        SubmittedDocument adjustment = await CreateSubmittedAdjustmentAsync(seed, -2m);

        // Act
        Result<Guid> result = await PostAsync(adjustment.Id, adjustment.RowVersion, seed.UserId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        decimal balance = await dbContext.InventoryBalances
            .Where(item => item.WarehouseId == seed.WarehouseId && item.MaterialId == seed.MaterialId)
            .Select(item => item.Quantity).SingleAsync();
        balance.ShouldBe(3m);
        StockMovement movement = await dbContext.StockMovements.SingleAsync(item => item.DocumentId == adjustment.Id);
        movement.MovementType.ShouldBe(MovementType.AdjustmentOut);
        movement.QuantityDelta.ShouldBe(-2m);
        (await dbContext.InventoryAdjustments.SingleAsync(item => item.Id == adjustment.Id)).Status
            .ShouldBe(InventoryAdjustmentStatus.Posted);
    }

    [Fact]
    public async Task HardFreeze_Should_BlockIntersectingPosting_WithoutPartialLedgerChanges()
    {
        // Arrange
        M7Seed seed = await SeedAsync();
        await CreateAndPostOpeningAsync(seed, 5m);
        SubmittedDocument adjustment = await CreateSubmittedAdjustmentAsync(seed, -1m);
        await StartHardFreezeAsync(seed);

        // Act
        Result<Guid> result = await PostAsync(adjustment.Id, adjustment.RowVersion, seed.UserId);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("InventoryCounts.PostingBlocked");
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await dbContext.StockMovements.AnyAsync(item => item.DocumentId == adjustment.Id)).ShouldBeFalse();
        (await dbContext.WarehouseDocuments.SingleAsync(item => item.Id == adjustment.Id)).DocumentStatus
            .ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public async Task HardFreeze_Should_AllowPosting_WhenCountMembershipDoesNotOverlap()
    {
        M7Seed seed = await SeedAsync();
        await CreateAndPostOpeningAsync(seed, 5m);
        SubmittedDocument adjustment = await CreateSubmittedAdjustmentAsync(seed, 1m);
        Guid unrelatedMaterialId = await CreateSecondMaterialAsync(seed);
        (Guid countUserId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantInventoryCountPlanAsync(countUserId, seed.WarehouseId);
        Guid countId = await CreatePlannedSelectedCountAsync(seed, unrelatedMaterialId);
        Authenticate(tokens.AccessToken);

        HttpResponseMessage start = await HttpClient.PostAsJsonAsync(
            $"inventory-counts/{countId}/start", new { expectedRowVersion = 1 });
        start.StatusCode.ShouldBe(HttpStatusCode.OK, await start.Content.ReadAsStringAsync());

        Result<PostingOutcome> posted = await PostWithOutcomeAsync(
            adjustment.Id, adjustment.RowVersion, seed.UserId);

        posted.IsSuccess.ShouldBeTrue(posted.IsFailure ? posted.Error.ToString() : null);
        posted.Value.Warnings.ShouldBeEmpty();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.StockMovements.AnyAsync(item => item.DocumentId == adjustment.Id)).ShouldBeTrue();
        (await context.WarehouseDocuments.SingleAsync(item => item.Id == adjustment.Id)).DocumentStatus
            .ShouldBe(DocumentStatus.Posted);
    }

    [Fact]
    public async Task HardFreeze_Should_BlockMultiMaterialPostingOnAnyPartialIntersection()
    {
        M7Seed seed = await SeedAsync();
        Guid unrelatedMaterialId = await CreateSecondMaterialAsync(seed);
        SubmittedDocument adjustment = await CreateSubmittedMultiMaterialAdjustmentAsync(seed, unrelatedMaterialId);
        await StartHardFreezeAsync(seed);

        Result<Guid> posted = await PostAsync(adjustment.Id, adjustment.RowVersion, seed.UserId);

        posted.IsFailure.ShouldBeTrue();
        posted.Error.Code.ShouldBe("InventoryCounts.PostingBlocked");
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.StockMovements.AnyAsync(item => item.DocumentId == adjustment.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task ConcurrentCountStartAndPosting_ShouldHonorExactMembershipUnderWarehouseLock()
    {
        M7Seed seed = await SeedAsync();
        await CreateAndPostOpeningAsync(seed, 5m);
        SubmittedDocument adjustment = await CreateSubmittedAdjustmentAsync(seed, -1m);
        (Guid countUserId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantInventoryCountPlanAsync(countUserId, seed.WarehouseId);
        Guid countId = await CreatePlannedSelectedCountAsync(seed, seed.MaterialId);
        Authenticate(tokens.AccessToken);

        Task<HttpResponseMessage> startTask = HttpClient.PostAsJsonAsync(
            $"inventory-counts/{countId}/start", new { expectedRowVersion = 1 });
        Task<Result<PostingOutcome>> postTask = PostWithOutcomeAsync(
            adjustment.Id, adjustment.RowVersion, seed.UserId);
        await Task.WhenAll(startTask, postTask);
        using HttpResponseMessage start = await startTask;
        Result<PostingOutcome> posted = await postTask;
        start.StatusCode.ShouldBe(HttpStatusCode.OK, await start.Content.ReadAsStringAsync());

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        decimal finalBalance = await context.InventoryBalances
            .Where(item => item.WarehouseId == seed.WarehouseId && item.MaterialId == seed.MaterialId)
            .Select(item => item.Quantity)
            .SingleAsync();
        decimal snapshot = await context.InventoryCountLines
            .Where(item => item.CountId == countId && item.MaterialId == seed.MaterialId)
            .Select(item => item.SnapshotQuantity)
            .SingleAsync();

        if (posted.IsSuccess)
        {
            // Posting won the warehouse lock first; Start must snapshot its committed result.
            finalBalance.ShouldBe(4m);
            snapshot.ShouldBe(finalBalance);
        }
        else
        {
            // Start won the warehouse lock first; exact membership blocks the intersecting post.
            posted.Error.Code.ShouldBe("InventoryCounts.PostingBlocked");
            finalBalance.ShouldBe(5m);
            snapshot.ShouldBe(finalBalance);
        }
    }

    [Fact]
    public async Task HardFreeze_Should_BlockReversalFromImmutableSourceMovementIntersection()
    {
        M7Seed seed = await SeedAsync();
        await CreateAndPostOpeningAsync(seed, 5m);
        SubmittedDocument source = await CreateSubmittedAdjustmentAsync(seed, 1m);
        (await PostAsync(source.Id, source.RowVersion, seed.UserId)).IsSuccess.ShouldBeTrue();
        await StartHardFreezeAsync(seed);

        (Guid reversalUserId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantWarehouseDocumentCreateAsync(reversalUserId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);
        HttpResponseMessage createReversal = await HttpClient.PostAsync(
            $"warehouse-documents/{source.Id}/reversals", null);
        createReversal.StatusCode.ShouldBe(HttpStatusCode.Created, await createReversal.Content.ReadAsStringAsync());
        ApiEnvelope<ResourceIdDto>? reversalEnvelope = await createReversal.Content
            .ReadFromJsonAsync<ApiEnvelope<ResourceIdDto>>();
        reversalEnvelope.ShouldNotBeNull();
        await SubmitReversalAsync(seed, reversalEnvelope.Data.Id);

        int reversalRowVersion;
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            reversalRowVersion = await context.WarehouseDocuments
                .Where(item => item.Id == reversalEnvelope.Data.Id)
                .Select(item => item.RowVersion)
                .SingleAsync();
        }

        Result<Guid> posted = await PostAsync(reversalEnvelope.Data.Id, reversalRowVersion, seed.UserId);

        posted.IsFailure.ShouldBeTrue();
        posted.Error.Code.ShouldBe("InventoryCounts.PostingBlocked");
        await using AsyncServiceScope assertScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext assertContext = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await assertContext.StockMovements.AnyAsync(item => item.DocumentId == reversalEnvelope.Data.Id))
            .ShouldBeFalse();
        (await assertContext.WarehouseDocuments.SingleAsync(item => item.Id == source.Id)).DocumentStatus
            .ShouldBe(DocumentStatus.Posted);
        (await assertContext.InventoryBalances.Where(item => item.WarehouseId == seed.WarehouseId &&
            item.MaterialId == seed.MaterialId).Select(item => item.Quantity).SingleAsync()).ShouldBe(6m);
    }

    [Fact]
    public async Task NoFreeze_Should_NotBlockPosting()
    {
        // Arrange
        M7Seed seed = await SeedAsync();
        await CreateAndPostOpeningAsync(seed, 5m);
        SubmittedDocument adjustment = await CreateSubmittedAdjustmentAsync(seed, 1m);
        await StartCountAsync(seed, FreezePolicy.NoFreeze);

        // Act
        Result<PostingOutcome> result = await PostWithOutcomeAsync(
            adjustment.Id,
            adjustment.RowVersion,
            seed.UserId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public async Task SoftFreeze_Should_PostAndReturnObservableWarning()
    {
        // Arrange
        M7Seed seed = await SeedAsync();
        await CreateAndPostOpeningAsync(seed, 5m);
        SubmittedDocument adjustment = await CreateSubmittedAdjustmentAsync(seed, 1m);
        await StartCountAsync(seed, FreezePolicy.SoftFreeze);

        // Act
        Result<PostingOutcome> result = await PostWithOutcomeAsync(
            adjustment.Id,
            adjustment.RowVersion,
            seed.UserId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        PostingWarning warning = result.Value.Warnings.ShouldHaveSingleItem();
        warning.Code.ShouldBe("InventoryCounts.SoftFreezeActive");
        warning.WarehouseId.ShouldBe(seed.WarehouseId);
    }

    [Fact]
    public async Task FreezeStatus_Should_ReturnSoftFreezeThroughHttpEnvelope()
    {
        // Arrange
        M7Seed seed = await SeedAsync();
        await StartCountAsync(seed, FreezePolicy.SoftFreeze);
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantInventoryCountViewAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            $"warehouses/{seed.WarehouseId}/inventory-freeze-status");

        // Assert
        response.EnsureSuccessStatusCode();
        ApiEnvelope<FreezeStatusDto>? envelope =
            await response.Content.ReadFromJsonAsync<ApiEnvelope<FreezeStatusDto>>();
        envelope.ShouldNotBeNull();
        envelope.Success.ShouldBeTrue();
        envelope.Data.IsPostingBlocked.ShouldBeFalse();
        envelope.Data.IsProvisional.ShouldBeTrue();
        envelope.Data.HasSoftFreezeWarning.ShouldBeTrue();
        envelope.Data.ActiveCounts.ShouldHaveSingleItem().FreezePolicy.ShouldBe(FreezePolicy.SoftFreeze);
    }

    [Fact]
    public async Task FreezeStatus_Should_ExposeHardFreezeAsProvisionalNotDefinitiveBlock()
    {
        M7Seed seed = await SeedAsync();
        await StartHardFreezeAsync(seed);
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantInventoryCountViewAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);

        HttpResponseMessage response = await HttpClient.GetAsync(
            $"warehouses/{seed.WarehouseId}/inventory-freeze-status");

        response.EnsureSuccessStatusCode();
        ApiEnvelope<FreezeStatusDto>? envelope =
            await response.Content.ReadFromJsonAsync<ApiEnvelope<FreezeStatusDto>>();
        envelope.ShouldNotBeNull();
        envelope.Success.ShouldBeTrue();
        envelope.Data.IsPostingBlocked.ShouldBeFalse();
        envelope.Data.IsProvisional.ShouldBeTrue();
        envelope.Data.HasSoftFreezeWarning.ShouldBeFalse();
        envelope.Data.ActiveCounts.ShouldHaveSingleItem().FreezePolicy.ShouldBe(FreezePolicy.HardFreeze);
    }

    [Fact]
    public async Task PostDocument_Should_ReturnSoftFreezeWarningThroughHttpEnvelope()
    {
        // Arrange
        M7Seed seed = await SeedAsync();
        await CreateAndPostOpeningAsync(seed, 5m);
        SubmittedDocument adjustment = await CreateSubmittedAdjustmentAsync(seed, 1m);
        await StartCountAsync(seed, FreezePolicy.SoftFreeze);
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantInventoryCountViewAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            $"warehouse-documents/{adjustment.Id}/post",
            new { expectedRowVersion = adjustment.RowVersion });

        // Assert
        response.EnsureSuccessStatusCode();
        ApiEnvelope<PostDocumentDto>? envelope =
            await response.Content.ReadFromJsonAsync<ApiEnvelope<PostDocumentDto>>();
        envelope.ShouldNotBeNull();
        envelope.Success.ShouldBeTrue();
        envelope.Data.DocumentId.ShouldBe(adjustment.Id);
        PostingWarningDto warning = envelope.Data.Warnings.ShouldHaveSingleItem();
        warning.Code.ShouldBe("InventoryCounts.SoftFreezeActive");
        warning.WarehouseId.ShouldBe(seed.WarehouseId);
    }

    [Fact]
    public async Task InStockAssetDisposal_Should_DecrementBalanceAndBecomeTerminal()
    {
        // Arrange
        M7Seed seed = await SeedAsync(assetTracked: true);
        await CreateAndPostOpeningAsync(seed, 1m);
        await using AsyncServiceScope setupScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Asset asset = await setupContext.Assets.SingleAsync(item => item.MaterialId == seed.MaterialId);
        SubmittedDocument disposal = await CreateSubmittedDisposalAsync(seed, asset.Id);

        // Act
        Result<Guid> result = await PostAsync(disposal.Id, disposal.RowVersion, seed.UserId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await using AsyncServiceScope assertScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        AssetMovementHistory latest = await dbContext.AssetMovementHistories
            .Where(item => item.AssetId == asset.Id)
            .OrderByDescending(item => item.MovedAtUtc).ThenByDescending(item => item.Id)
            .FirstAsync();
        latest.MovementType.ShouldBe(AssetMovementType.Disposed);
        decimal balance = await dbContext.InventoryBalances
            .Where(item => item.WarehouseId == seed.WarehouseId && item.MaterialId == seed.MaterialId)
            .Select(item => item.Quantity).SingleAsync();
        balance.ShouldBe(0m);
    }

    [Fact]
    public async Task CustodiedAssetDisposal_Should_CloseCustodyWithoutSecondStockDecrement()
    {
        // Arrange
        M7Seed seed = await SeedAsync(assetTracked: true);
        await CreateAndPostOpeningAsync(seed, 1m);
        await using AsyncServiceScope setupScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Asset asset = await setupContext.Assets.SingleAsync(item => item.MaterialId == seed.MaterialId);
        await CreateAndPostAssetIssueAsync(seed, asset.Id);
        SubmittedDocument disposal = await CreateSubmittedDisposalAsync(seed, asset.Id, -0m);

        // Act
        Result<Guid> result = await PostAsync(disposal.Id, disposal.RowVersion, seed.UserId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await using AsyncServiceScope assertScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Custody custody = await dbContext.Custodies.SingleAsync(item => item.AssetId == asset.Id);
        custody.Status.ShouldBe(CustodyStatus.Closed);
        custody.DisposalDocumentId.ShouldBe(disposal.Id);
        (await dbContext.StockMovements.AnyAsync(item => item.DocumentId == disposal.Id)).ShouldBeFalse();
        decimal balance = await dbContext.InventoryBalances
            .Where(item => item.WarehouseId == seed.WarehouseId && item.MaterialId == seed.MaterialId)
            .Select(item => item.Quantity).SingleAsync();
        balance.ShouldBe(0m);
    }

    [Fact]
    public async Task ConcurrentDisposals_Should_AllowExactlyOneWinnerForSameAsset()
    {
        // Arrange
        M7Seed seed = await SeedAsync(assetTracked: true);
        await CreateAndPostOpeningAsync(seed, 1m);
        await using AsyncServiceScope setupScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Asset asset = await setupContext.Assets.SingleAsync(item => item.MaterialId == seed.MaterialId);
        SubmittedDocument first = await CreateSubmittedDisposalAsync(seed, asset.Id);
        SubmittedDocument second = await CreateSubmittedDisposalAsync(seed, asset.Id);

        // Act
        Result<Guid>[] results = await Task.WhenAll(
            PostAsync(first.Id, first.RowVersion, seed.UserId),
            PostAsync(second.Id, second.RowVersion, seed.UserId));

        // Assert
        results.Count(result => result.IsSuccess).ShouldBe(1);
        Result<Guid> loser = results.Single(result => result.IsFailure);
        loser.Error.Code.ShouldBe("Disposals.AssetAlreadyDisposed");
        await using AsyncServiceScope assertScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.AssetMovementHistories.CountAsync(item =>
            item.AssetId == asset.Id && item.MovementType == AssetMovementType.Disposed)).ShouldBe(1);
    }

    [Fact]
    public async Task CreateReversal_Should_ReturnExactConflict_ForPostedDisposal()
    {
        // Arrange
        M7Seed seed = await SeedAsync(assetTracked: true);
        await CreateAndPostOpeningAsync(seed, 1m);
        await using AsyncServiceScope setupScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Asset asset = await setupContext.Assets.SingleAsync(item => item.MaterialId == seed.MaterialId);
        SubmittedDocument disposal = await CreateSubmittedDisposalAsync(seed, asset.Id);
        (await PostAsync(disposal.Id, disposal.RowVersion, seed.UserId)).IsSuccess.ShouldBeTrue();
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantWarehouseDocumentCreateAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsync(
            $"adjustments/{disposal.Id}/reverse", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        ApiErrorEnvelope? envelope = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        envelope.ShouldNotBeNull();
        envelope.Success.ShouldBeFalse();
        envelope.Error.Code.ShouldBe("DISPOSALS_REVERSAL_NOT_ALLOWED");
    }

    [Fact]
    public async Task CreateDisposal_Should_CreateBatchThroughHttp()
    {
        // Arrange
        M7Seed seed = await SeedAsync(assetTracked: true);
        await CreateAndPostOpeningAsync(seed, 1m);
        await using AsyncServiceScope setupScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Asset asset = await setupContext.Assets.SingleAsync(item => item.MaterialId == seed.MaterialId);
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantWarehouseDocumentPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "adjustments/disposals",
            new { warehouseId = seed.WarehouseId, assetIds = new[] { asset.Id }, reason = "Damaged" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        ApiEnvelope<ResourceIdDto>? envelope =
            await response.Content.ReadFromJsonAsync<ApiEnvelope<ResourceIdDto>>();
        envelope.ShouldNotBeNull();
        await using AsyncServiceScope assertScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        InventoryAdjustment adjustment = await context.InventoryAdjustments.SingleAsync(
            item => item.Id == envelope.Data.Id);
        adjustment.AdjustmentKind.ShouldBe(AdjustmentKind.Disposal);
        (await context.DocumentLines.CountAsync(item => item.DocumentId == adjustment.Id)).ShouldBe(1);
        (await context.DocumentLineAssetSelections.CountAsync(item => item.DocumentId == adjustment.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task AdjustmentLineMutations_Should_RemainAtomicThroughHttp()
    {
        // Arrange
        M7Seed seed = await SeedAsync();
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantWarehouseDocumentPermissionsAsync(userId, seed.WarehouseId);
        Authenticate(tokens.AccessToken);
        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync(
            "adjustments", new { warehouseId = seed.WarehouseId, reason = "Variance" });
        createResponse.EnsureSuccessStatusCode();
        ApiEnvelope<ResourceIdDto>? created =
            await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<ResourceIdDto>>();
        created.ShouldNotBeNull();
        Guid documentId = created.Data.Id;

        // Act
        HttpResponseMessage addResponse = await HttpClient.PostAsJsonAsync(
            $"adjustments/{documentId}/lines",
            new
            {
                materialId = seed.MaterialId,
                difference = -2m,
                unitId = seed.UnitId,
                reason = "Shortage",
                expectedRowVersion = 1
            });
        addResponse.EnsureSuccessStatusCode();
        ApiEnvelope<ResourceIdDto>? added =
            await addResponse.Content.ReadFromJsonAsync<ApiEnvelope<ResourceIdDto>>();
        added.ShouldNotBeNull();
        HttpResponseMessage updateResponse = await HttpClient.PutAsJsonAsync(
            $"adjustments/{documentId}/lines/{added.Data.Id}",
            new { difference = 3m, unitId = seed.UnitId, reason = "Surplus", expectedRowVersion = 2 });
        updateResponse.EnsureSuccessStatusCode();
        HttpResponseMessage removeResponse = await HttpClient.DeleteAsync(
            $"adjustments/{documentId}/lines/{added.Data.Id}?expectedRowVersion=3");

        // Assert
        removeResponse.EnsureSuccessStatusCode();
        await using AsyncServiceScope assertScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.DocumentLines.AnyAsync(item => item.Id == added.Data.Id)).ShouldBeFalse();
        (await context.AdjustmentLines.AnyAsync(item => item.Id == added.Data.Id)).ShouldBeFalse();
    }

    private async Task<M7Seed> SeedAsync(bool assetTracked = false)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var materialId = Guid.NewGuid();

        dbContext.Users.Add(User.Create(userId, $"m7-{suffix}@example.com", "M7", "Tester", "hash"));
        dbContext.Organizations.Add(Organization.Create(organizationId, $"Org {suffix}", $"O{suffix}"));
        dbContext.Sites.Add(Site.Create(siteId, organizationId, $"Site {suffix}", $"S{suffix}", null));
        dbContext.Warehouses.Add(Warehouse.Create(warehouseId, siteId, $"Warehouse {suffix}", $"W{suffix}", "Main", true));
        dbContext.UnitsOfMeasure.Add(UnitOfMeasure.Create(unitId, $"Piece {suffix}", $"P{suffix}", "Count"));
        dbContext.MaterialDomains.Add(MaterialDomain.Create(domainId, $"Domain {suffix}", $"D{suffix}"));
        dbContext.MaterialCategories.Add(MaterialCategory.Create(categoryId, domainId, null, $"Category {suffix}", $"C{suffix}"));
        dbContext.MaterialFamilies.Add(MaterialFamily.Create(familyId, categoryId, $"Family {suffix}", $"F{suffix}"));
        dbContext.Materials.Add(Material.Create(materialId, familyId, unitId, $"Material {suffix}", null,
            $"M{suffix}", assetTracked ? MaterialKind.Asset : MaterialKind.Consumable,
            assetTracked ? TrackingType.Serial : TrackingType.Quantity, false, null));
        var capability = WarehouseCapability.Create(Guid.NewGuid(), warehouseId, domainId);
        dbContext.WarehouseCapabilities.Add(capability);
        dbContext.WarehouseCapabilityOperations.AddRange(
            WarehouseCapabilityOperation.Create(Guid.NewGuid(), capability.Id, OperationType.Receiving),
            WarehouseCapabilityOperation.Create(Guid.NewGuid(), capability.Id, OperationType.Issue),
            WarehouseCapabilityOperation.Create(Guid.NewGuid(), capability.Id, OperationType.Adjustment),
            WarehouseCapabilityOperation.Create(Guid.NewGuid(), capability.Id, OperationType.Count));
        await dbContext.SaveChangesAsync();
        return new M7Seed(userId, siteId, warehouseId, unitId, materialId, assetTracked);
    }

    private async Task CreateAndPostOpeningAsync(M7Seed seed, decimal quantity)
    {
        SubmittedDocument document = await CreateSubmittedDocumentAsync(
            seed, DocumentType.Opening, quantity, OpeningType.Initial, null);
        Result<Guid> result = await PostAsync(document.Id, document.RowVersion, seed.UserId);
        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.ToString() : null);
    }

    private async Task<SubmittedDocument> CreateSubmittedAdjustmentAsync(M7Seed seed, decimal difference)
    {
        return await CreateSubmittedDocumentAsync(seed, DocumentType.Adjustment,
            Math.Abs(difference), null, difference);
    }

    private async Task<SubmittedDocument> CreateSubmittedDocumentAsync(
        M7Seed seed,
        DocumentType type,
        decimal quantity,
        OpeningType? openingType,
        decimal? difference)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var document = WarehouseDocument.CreateDraft(Guid.NewGuid(), seed.WarehouseId, type, $"{type}-{suffix}");
        Material material = await dbContext.Materials.SingleAsync(item => item.Id == seed.MaterialId);
        DocumentLineProvenance provenance = DocumentLineProvenance.Create(
            material.CatalogVersion, material.MaterialKind, material.TrackingType, material.BaseUnitId).Value;
        Result<DocumentLine> line = DocumentLine.Create(Guid.NewGuid(), document.Id, seed.MaterialId,
            seed.AssetTracked ? DocumentLineType.Asset : DocumentLineType.Normal,
            quantity, seed.UnitId, quantity, null, null, null, openingType, provenance: provenance);
        dbContext.WarehouseDocuments.Add(document);
        dbContext.DocumentLines.Add(line.Value);
        if (difference is decimal adjustmentDifference)
        {
            dbContext.InventoryAdjustments.Add(InventoryAdjustment.Create(
                document.Id, null, AdjustmentKind.Quantity, "Count variance").Value);
            dbContext.AdjustmentLines.Add(AdjustmentLine.Create(
                line.Value.Id, document.Id, adjustmentDifference, "Count variance").Value);
        }

        var attachment = DocumentAttachment.Create(Guid.NewGuid(), document.Id,
            AttachmentType.SignedOriginal, $"m7/{suffix}.pdf", $"{suffix}.pdf",
            "application/pdf", 1, suffix, seed.UserId, DateTime.UtcNow);
        dbContext.DocumentAttachments.Add(attachment);
        await dbContext.SaveChangesAsync();
        document.SetSignedCopy(attachment.Id).IsSuccess.ShouldBeTrue();
        document.UpdatePaperReference($"P-{suffix}", 2026).IsSuccess.ShouldBeTrue();
        document.Submit().IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync();
        return new SubmittedDocument(document.Id, document.RowVersion);
    }

    private async Task<SubmittedDocument> CreateSubmittedMultiMaterialAdjustmentAsync(
        M7Seed seed,
        Guid secondMaterialId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), seed.WarehouseId, DocumentType.Adjustment, $"ADJ-MULTI-{suffix}");
        InventoryAdjustment adjustment = InventoryAdjustment.Create(
            document.Id, null, AdjustmentKind.Quantity, "Two-material variance").Value;
        context.WarehouseDocuments.Add(document);
        context.InventoryAdjustments.Add(adjustment);

        foreach (Guid materialId in new[] { seed.MaterialId, secondMaterialId })
        {
            Material material = await context.Materials.SingleAsync(item => item.Id == materialId);
            DocumentLine line = DocumentLine.Create(
                Guid.NewGuid(), document.Id, materialId, DocumentLineType.Normal, 1m, seed.UnitId, 1m,
                null, null, null,
                provenance: DocumentLineProvenance.Create(
                    material.CatalogVersion, material.MaterialKind, material.TrackingType, material.BaseUnitId).Value).Value;
            context.DocumentLines.Add(line);
            context.AdjustmentLines.Add(AdjustmentLine.Create(
                line.Id, document.Id, 1m, "Surplus").Value);
        }

        var attachment = DocumentAttachment.Create(
            Guid.NewGuid(), document.Id, AttachmentType.SignedOriginal, $"m7/{suffix}.pdf", $"{suffix}.pdf",
            "application/pdf", 1, suffix, seed.UserId, DateTime.UtcNow);
        context.DocumentAttachments.Add(attachment);
        await context.SaveChangesAsync();
        document.SetSignedCopy(attachment.Id).IsSuccess.ShouldBeTrue();
        document.UpdatePaperReference($"P-{suffix}", DateTime.UtcNow.Year).IsSuccess.ShouldBeTrue();
        document.Submit().IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync();
        return new SubmittedDocument(document.Id, document.RowVersion);
    }

    private async Task<SubmittedDocument> CreateSubmittedDisposalAsync(M7Seed seed, Guid assetId, decimal difference = -1m)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), seed.WarehouseId, DocumentType.Adjustment, $"DIS-{suffix}");
        Material material = await dbContext.Materials.SingleAsync(item => item.Id == seed.MaterialId);
        DocumentLineProvenance provenance = DocumentLineProvenance.Create(
            material.CatalogVersion, material.MaterialKind, material.TrackingType, material.BaseUnitId).Value;
        Result<DocumentLine> line = DocumentLine.Create(Guid.NewGuid(), document.Id, seed.MaterialId,
            DocumentLineType.Asset, 1m, seed.UnitId, 1m, null, null, null, provenance: provenance);
        dbContext.WarehouseDocuments.Add(document);
        dbContext.DocumentLines.Add(line.Value);
        dbContext.InventoryAdjustments.Add(InventoryAdjustment.Create(
            document.Id, null, AdjustmentKind.Disposal, "Unserviceable asset").Value);
        dbContext.AdjustmentLines.Add(AdjustmentLine.Create(
            line.Value.Id, document.Id, difference, "Unserviceable asset", allowZero: true).Value);
        dbContext.DocumentLineAssetSelections.Add(DocumentLineAssetSelection.Create(
            Guid.NewGuid(), document.Id, line.Value.Id, assetId).Value);
        var attachment = DocumentAttachment.Create(Guid.NewGuid(), document.Id,
            AttachmentType.SignedOriginal, $"m7/{suffix}.pdf", $"{suffix}.pdf",
            "application/pdf", 1, suffix, seed.UserId, DateTime.UtcNow);
        dbContext.DocumentAttachments.Add(attachment);
        await dbContext.SaveChangesAsync();
        document.SetSignedCopy(attachment.Id).IsSuccess.ShouldBeTrue();
        document.UpdatePaperReference($"P-{suffix}", 2026).IsSuccess.ShouldBeTrue();
        document.Submit().IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync();
        return new SubmittedDocument(document.Id, document.RowVersion);
    }

    private async Task SubmitReversalAsync(M7Seed seed, Guid reversalId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        WarehouseDocument reversal = await context.WarehouseDocuments.SingleAsync(item => item.Id == reversalId);
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var attachment = DocumentAttachment.Create(
            Guid.NewGuid(), reversal.Id, AttachmentType.SignedOriginal,
            $"m7/reversal/{suffix}.pdf", $"{suffix}.pdf", "application/pdf", 1, suffix,
            seed.UserId, DateTime.UtcNow);
        context.DocumentAttachments.Add(attachment);
        await context.SaveChangesAsync();
        reversal.SetSignedCopy(attachment.Id).IsSuccess.ShouldBeTrue();
        reversal.UpdatePaperReference($"REV-{suffix}", DateTime.UtcNow.Year).IsSuccess.ShouldBeTrue();
        reversal.Submit().IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync();
    }

    private async Task CreateAndPostAssetIssueAsync(M7Seed seed, Guid assetId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), seed.WarehouseId, DocumentType.Issue, $"ISS-{suffix}");
        Material material = await dbContext.Materials.SingleAsync(item => item.Id == seed.MaterialId);
        DocumentLineProvenance provenance = DocumentLineProvenance.Create(
            material.CatalogVersion, material.MaterialKind, material.TrackingType, material.BaseUnitId).Value;
        Result<DocumentLine> line = DocumentLine.Create(Guid.NewGuid(), document.Id, seed.MaterialId,
            DocumentLineType.Asset, 1m, seed.UnitId, 1m, null, null, null, provenance: provenance);
        dbContext.WarehouseDocuments.Add(document);
        dbContext.DocumentLines.Add(line.Value);
        dbContext.IssueTos.Add(IssueTo.Create(
            document.Id, PartyType.Site, seed.SiteId, "Operational issue").Value);
        dbContext.DocumentLineAssetSelections.Add(DocumentLineAssetSelection.Create(
            Guid.NewGuid(), document.Id, line.Value.Id, assetId).Value);
        var attachment = DocumentAttachment.Create(Guid.NewGuid(), document.Id,
            AttachmentType.SignedOriginal, $"m7/{suffix}.pdf", $"{suffix}.pdf",
            "application/pdf", 1, suffix, seed.UserId, DateTime.UtcNow);
        dbContext.DocumentAttachments.Add(attachment);
        await dbContext.SaveChangesAsync();
        document.SetSignedCopy(attachment.Id).IsSuccess.ShouldBeTrue();
        document.UpdatePaperReference($"P-{suffix}", 2026).IsSuccess.ShouldBeTrue();
        document.Submit().IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync();
        (await PostAsync(document.Id, document.RowVersion, seed.UserId)).IsSuccess.ShouldBeTrue();
    }

    private Task StartHardFreezeAsync(M7Seed seed) => StartCountAsync(seed, FreezePolicy.HardFreeze);

    private async Task<Guid> CreatePlannedSelectedCountAsync(M7Seed seed, Guid materialId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        InventoryCount count = InventoryCount.Plan(
            Guid.NewGuid(),
            seed.WarehouseId,
            seed.UserId,
            InventoryCountType.Surprise,
            InventoryCountScopeType.SelectedMaterials,
            null,
            FreezePolicy.HardFreeze,
            DateTime.UtcNow).Value;
        context.InventoryCounts.Add(count);
        context.InventoryCountScopeMaterials.Add(
            InventoryCountScopeMaterial.Create(Guid.NewGuid(), count.Id, materialId));
        await context.SaveChangesAsync();
        return count.Id;
    }

    private async Task<Guid> CreateSecondMaterialAsync(M7Seed seed)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Material existing = await context.Materials.SingleAsync(item => item.Id == seed.MaterialId);
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var material = Material.Create(
            Guid.NewGuid(),
            existing.FamilyId,
            seed.UnitId,
            $"Uncounted material {suffix}",
            null,
            $"UM{suffix}",
            MaterialKind.Consumable,
            TrackingType.Quantity,
            false,
            null);
        context.Materials.Add(material);
        await context.SaveChangesAsync();
        return material.Id;
    }

    private async Task GrantInventoryCountPlanAsync(Guid userId, Guid warehouseId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleId = Guid.NewGuid();
        context.Roles.Add(Role.Create(roleId, $"M7 count planner {roleId:N}", "دور اختباري", null));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(roleId, ScopeType.Warehouse));
        context.RolePermissions.Add(RolePermission.Create(roleId, WellKnownDottedPermissions.CountPlanId));
        await context.UserRoleScopes.Where(item => item.UserId == userId).ExecuteDeleteAsync();
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(), userId, roleId, ScopeType.Warehouse, warehouseId));
        await context.SaveChangesAsync();
    }

    private async Task StartCountAsync(M7Seed seed, FreezePolicy policy)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        InventoryCount count = InventoryCount.Plan(Guid.NewGuid(), seed.WarehouseId, seed.UserId,
            InventoryCountType.Surprise, InventoryCountScopeType.EntireWarehouse,
            null, policy, DateTime.UtcNow).Value;
        dbContext.InventoryCountLines.Add(InventoryCountLine.Create(
            Guid.NewGuid(), count.Id, seed.MaterialId, null, seed.AssetTracked ? 1m : 0m).Value);
        dbContext.InventoryCounts.Add(count);
        await dbContext.SaveChangesAsync();
        count.Start(DateTime.UtcNow.AddTicks(1)).IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync();
    }

    private async Task<Result<Guid>> PostAsync(Guid documentId, int rowVersion, Guid userId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
        Result<PostingOutcome> result = await coordinator.PostAsync(
            documentId, rowVersion, userId, CancellationToken.None);
        return result.IsFailure
            ? Result.Failure<Guid>(result.Error)
            : result.Value.DocumentId;
    }

    private async Task<Result<PostingOutcome>> PostWithOutcomeAsync(
        Guid documentId,
        int rowVersion,
        Guid userId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IDocumentPostingCoordinator coordinator =
            scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
        return await coordinator.PostAsync(documentId, rowVersion, userId, CancellationToken.None);
    }

    private async Task GrantInventoryCountViewAsync(Guid userId, Guid warehouseId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleId = Guid.NewGuid();
        context.Roles.Add(Role.Create(roleId, $"M7 freeze viewer {roleId:N}", "دور اختباري", null));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(roleId, ScopeType.Warehouse));
        context.RolePermissions.AddRange(
            RolePermission.Create(roleId, WellKnownDottedPermissions.CountViewId),
            RolePermission.Create(roleId, WellKnownDottedPermissions.DocumentPostId));
        await context.UserRoleScopes.Where(item => item.UserId == userId).ExecuteDeleteAsync();
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(),
            userId,
            roleId,
            ScopeType.Warehouse,
            warehouseId));
        await context.SaveChangesAsync();
    }

    private async Task GrantWarehouseDocumentCreateAsync(Guid userId, Guid warehouseId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleId = Guid.NewGuid();
        context.Roles.Add(Role.Create(roleId, $"M7 reversal {roleId:N}", "دور اختباري", null));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(roleId, ScopeType.Warehouse));
        context.RolePermissions.AddRange(
            RolePermission.Create(roleId, WellKnownDottedPermissions.DocumentCreateId),
            RolePermission.Create(roleId, WellKnownDottedPermissions.DocumentReverseId));
        await context.UserRoleScopes.Where(item => item.UserId == userId).ExecuteDeleteAsync();
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(), userId, roleId, ScopeType.Warehouse, warehouseId));
        await context.SaveChangesAsync();
    }

    private async Task GrantWarehouseDocumentPermissionsAsync(Guid userId, Guid warehouseId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleId = Guid.NewGuid();
        context.Roles.Add(Role.Create(roleId, $"M7 adjustment {roleId:N}", "دور اختباري", null));
        context.RoleAllowedScopeTypes.Add(RoleAllowedScopeType.Create(roleId, ScopeType.Warehouse));
        context.RolePermissions.AddRange(
            RolePermission.Create(roleId, WellKnownDottedPermissions.DocumentCreateId),
            RolePermission.Create(roleId, WellKnownDottedPermissions.DocumentUpdateId));
        await context.UserRoleScopes.Where(item => item.UserId == userId).ExecuteDeleteAsync();
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(), userId, roleId, ScopeType.Warehouse, warehouseId));
        await context.SaveChangesAsync();
    }

    private sealed record M7Seed(Guid UserId, Guid SiteId, Guid WarehouseId, Guid UnitId, Guid MaterialId, bool AssetTracked);
    private sealed record SubmittedDocument(Guid Id, int RowVersion);
    private sealed record ActiveFreezeDto(Guid CountId, FreezePolicy FreezePolicy);
    private sealed record FreezeStatusDto(
        Guid WarehouseId,
        bool IsPostingBlocked,
        bool IsProvisional,
        bool HasSoftFreezeWarning,
        IReadOnlyList<ActiveFreezeDto> ActiveCounts);
    private sealed record PostingWarningDto(
        string Code,
        string Message,
        Guid CountId,
        Guid WarehouseId);
    private sealed record PostDocumentDto(
        Guid DocumentId,
        IReadOnlyList<PostingWarningDto> Warnings);
    private sealed record ApiErrorEnvelope(bool Success, ApiError Error);
    private sealed record ApiError(string Code);
    private sealed record ResourceIdDto(Guid Id);
}

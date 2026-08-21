using Application.Abstractions.Posting;
using Domain.Common;
using Domain.DocumentAttachments;
using Domain.DocumentLines;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.InventoryCounts;
using Domain.Organizations;
using Domain.StockMovements;
using Domain.TransferInfos;
using Domain.Sites;
using Domain.UnitsOfMeasure;
using Domain.Users;
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
public sealed class M5TransferPostingTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task TransferPost_Should_MoveBothBalancesAndWriteAnOutInPair()
    {
        // Arrange
        M5Seed seed = await SeedAsync(includeDestinationTransferCapability: true);
        await CreateAndPostOpeningAsync(seed, 10m);
        SubmittedDocument transfer = await CreateSubmittedTransferAsync(seed, 4m);

        // Act
        Result<Guid> result = await PostAsync(transfer.Id, transfer.RowVersion, seed.UserId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await GetBalanceAsync(dbContext, seed.SourceWarehouseId, seed.MaterialId)).ShouldBe(6m);
        (await GetBalanceAsync(dbContext, seed.DestinationWarehouseId, seed.MaterialId)).ShouldBe(4m);
        List<StockMovement> movements = await dbContext.StockMovements
            .Where(item => item.DocumentId == transfer.Id).ToListAsync();
        movements.Count.ShouldBe(2);
        movements.ShouldContain(item => item.MovementType == MovementType.TransferOut && item.QuantityDelta == -4m);
        movements.ShouldContain(item => item.MovementType == MovementType.TransferIn && item.QuantityDelta == 4m);
    }

    [Fact]
    public async Task TransferPost_Should_RollBackBothSides_WhenSourceStockIsInsufficient()
    {
        // Arrange
        M5Seed seed = await SeedAsync(includeDestinationTransferCapability: true);
        await CreateAndPostOpeningAsync(seed, 3m);
        SubmittedDocument transfer = await CreateSubmittedTransferAsync(seed, 4m);

        // Act
        Result<Guid> result = await PostAsync(transfer.Id, transfer.RowVersion, seed.UserId);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("InventoryBalances.InsufficientQuantity");
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await GetBalanceAsync(dbContext, seed.SourceWarehouseId, seed.MaterialId)).ShouldBe(3m);
        (await dbContext.StockMovements.AnyAsync(item => item.DocumentId == transfer.Id)).ShouldBeFalse();
        (await dbContext.InventoryBalances.AnyAsync(item => item.WarehouseId == seed.DestinationWarehouseId && item.MaterialId == seed.MaterialId)).ShouldBeFalse();
    }

    [Fact]
    public async Task TransferPost_Should_RollBack_WhenDestinationCapabilityIsMissing()
    {
        // Arrange
        M5Seed seed = await SeedAsync(includeDestinationTransferCapability: false);
        await CreateAndPostOpeningAsync(seed, 3m);
        SubmittedDocument transfer = await CreateSubmittedTransferAsync(seed, 2m);

        // Act
        Result<Guid> result = await PostAsync(transfer.Id, transfer.RowVersion, seed.UserId);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("WarehouseCapabilities.NotGranted");
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await GetBalanceAsync(dbContext, seed.SourceWarehouseId, seed.MaterialId)).ShouldBe(3m);
        (await dbContext.StockMovements.AnyAsync(item => item.DocumentId == transfer.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task TransferPost_Should_BeBlocked_WhenDestinationHasHardFreeze()
    {
        // Arrange
        M5Seed seed = await SeedAsync(includeDestinationTransferCapability: true);
        await CreateAndPostOpeningAsync(seed, 5m);
        SubmittedDocument transfer = await CreateSubmittedTransferAsync(seed, 2m);
        await StartHardFreezeAsync(seed.DestinationWarehouseId, seed.UserId);

        // Act
        Result<Guid> result = await PostAsync(transfer.Id, transfer.RowVersion, seed.UserId);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("InventoryCounts.PostingBlocked");
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await dbContext.StockMovements.AnyAsync(item => item.DocumentId == transfer.Id)).ShouldBeFalse();
        (await GetBalanceAsync(dbContext, seed.SourceWarehouseId, seed.MaterialId)).ShouldBe(5m);
    }

    [Fact]
    public async Task ConcurrentTransfers_Should_AllowOnlyOneOutboundQuantityThatFits()
    {
        // Arrange
        M5Seed seed = await SeedAsync(includeDestinationTransferCapability: true);
        await CreateAndPostOpeningAsync(seed, 5m);
        SubmittedDocument first = await CreateSubmittedTransferAsync(seed, 3m);
        SubmittedDocument second = await CreateSubmittedTransferAsync(seed, 3m);

        // Act
        Result<Guid>[] results = await Task.WhenAll(
            PostAsync(first.Id, first.RowVersion, seed.UserId),
            PostAsync(second.Id, second.RowVersion, seed.UserId));

        // Assert
        results.Count(result => result.IsSuccess).ShouldBe(1);
        results.Single(result => result.IsFailure).Error.Code.ShouldBe("InventoryBalances.InsufficientQuantity");
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await GetBalanceAsync(dbContext, seed.SourceWarehouseId, seed.MaterialId)).ShouldBe(2m);
        (await GetBalanceAsync(dbContext, seed.DestinationWarehouseId, seed.MaterialId)).ShouldBe(3m);
    }

    [Fact]
    public async Task TransferReversal_Should_RestoreBothBalancesAndMarkSourceReversed()
    {
        // Arrange
        M5Seed seed = await SeedAsync(includeDestinationTransferCapability: true);
        await CreateAndPostOpeningAsync(seed, 10m);
        SubmittedDocument transfer = await CreateSubmittedTransferAsync(seed, 4m);
        (await PostAsync(transfer.Id, transfer.RowVersion, seed.UserId)).IsSuccess.ShouldBeTrue();
        SubmittedDocument reversal = await CreateSubmittedReversalAsync(seed, transfer.Id);

        // Act
        Result<Guid> result = await PostAsync(reversal.Id, reversal.RowVersion, seed.UserId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await dbContext.WarehouseDocuments.SingleAsync(item => item.Id == transfer.Id)).DocumentStatus
            .ShouldBe(DocumentStatus.Reversed);
        (await dbContext.WarehouseDocuments.SingleAsync(item => item.Id == reversal.Id)).DocumentStatus
            .ShouldBe(DocumentStatus.Posted);
        (await GetBalanceAsync(dbContext, seed.SourceWarehouseId, seed.MaterialId)).ShouldBe(10m);
        (await GetBalanceAsync(dbContext, seed.DestinationWarehouseId, seed.MaterialId)).ShouldBe(0m);
        List<StockMovement> reversalMovements = await dbContext.StockMovements
            .Where(item => item.DocumentId == reversal.Id)
            .ToListAsync();
        reversalMovements.Count.ShouldBe(2);
        reversalMovements.ShouldContain(item =>
            item.WarehouseId == seed.SourceWarehouseId &&
            item.MovementType == MovementType.TransferOut &&
            item.QuantityDelta == 4m);
        reversalMovements.ShouldContain(item =>
            item.WarehouseId == seed.DestinationWarehouseId &&
            item.MovementType == MovementType.TransferIn &&
            item.QuantityDelta == -4m);
    }

    [Fact]
    public async Task TransferReversal_Should_RollBack_WhenDestinationQuantityWasConsumed()
    {
        // Arrange
        M5Seed seed = await SeedAsync(includeDestinationTransferCapability: true);
        await CreateAndPostOpeningAsync(seed, 10m);
        SubmittedDocument originalTransfer = await CreateSubmittedTransferAsync(seed, 4m);
        (await PostAsync(originalTransfer.Id, originalTransfer.RowVersion, seed.UserId)).IsSuccess.ShouldBeTrue();
        SubmittedDocument downstreamTransfer = await CreateSubmittedTransferAsync(
            seed,
            seed.DestinationWarehouseId,
            seed.SourceWarehouseId,
            2m);
        (await PostAsync(downstreamTransfer.Id, downstreamTransfer.RowVersion, seed.UserId)).IsSuccess.ShouldBeTrue();
        SubmittedDocument reversal = await CreateSubmittedReversalAsync(seed, originalTransfer.Id);

        // Act
        Result<Guid> result = await PostAsync(reversal.Id, reversal.RowVersion, seed.UserId);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("InventoryBalances.InsufficientQuantity");
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await dbContext.WarehouseDocuments.SingleAsync(item => item.Id == originalTransfer.Id)).DocumentStatus
            .ShouldBe(DocumentStatus.Posted);
        (await dbContext.WarehouseDocuments.SingleAsync(item => item.Id == reversal.Id)).DocumentStatus
            .ShouldBe(DocumentStatus.Submitted);
        (await dbContext.StockMovements.AnyAsync(item => item.DocumentId == reversal.Id)).ShouldBeFalse();
        (await GetBalanceAsync(dbContext, seed.SourceWarehouseId, seed.MaterialId)).ShouldBe(8m);
        (await GetBalanceAsync(dbContext, seed.DestinationWarehouseId, seed.MaterialId)).ShouldBe(2m);
    }

    [Fact]
    public async Task OppositeDirectionTransfers_Should_CompleteWithoutChangingEitherBalance()
    {
        // Arrange
        M5Seed seed = await SeedAsync(includeDestinationTransferCapability: true);
        await CreateAndPostOpeningAsync(seed, seed.SourceWarehouseId, 10m);
        await CreateAndPostOpeningAsync(seed, seed.DestinationWarehouseId, 10m);
        SubmittedDocument sourceToDestination = await CreateSubmittedTransferAsync(
            seed,
            seed.SourceWarehouseId,
            seed.DestinationWarehouseId,
            3m);
        SubmittedDocument destinationToSource = await CreateSubmittedTransferAsync(
            seed,
            seed.DestinationWarehouseId,
            seed.SourceWarehouseId,
            3m);

        // Act
        Result<Guid>[] results = await Task.WhenAll(
            PostAsync(sourceToDestination.Id, sourceToDestination.RowVersion, seed.UserId),
            PostAsync(destinationToSource.Id, destinationToSource.RowVersion, seed.UserId));

        // Assert
        results.All(result => result.IsSuccess).ShouldBeTrue();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await GetBalanceAsync(dbContext, seed.SourceWarehouseId, seed.MaterialId)).ShouldBe(10m);
        (await GetBalanceAsync(dbContext, seed.DestinationWarehouseId, seed.MaterialId)).ShouldBe(10m);
    }

    private async Task<M5Seed> SeedAsync(bool includeDestinationTransferCapability)
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sourceWarehouseId = Guid.NewGuid();
        var destinationWarehouseId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var materialId = Guid.NewGuid();

        dbContext.Users.Add(User.Create(userId, $"m5-{suffix}@example.com", "M5", "Tester", "hash"));
        dbContext.Organizations.Add(Organization.Create(organizationId, $"Org {suffix}", $"O{suffix}"));
        dbContext.Sites.Add(Site.Create(siteId, organizationId, $"Site {suffix}", $"S{suffix}", null));
        dbContext.Warehouses.AddRange(
            Warehouse.Create(sourceWarehouseId, siteId, $"Source {suffix}", $"WS{suffix}", "Main", true),
            Warehouse.Create(destinationWarehouseId, siteId, $"Dest {suffix}", $"WD{suffix}", "Main", true));
        dbContext.UnitsOfMeasure.Add(UnitOfMeasure.Create(unitId, $"Piece {suffix}", $"P{suffix}", "Count"));
        dbContext.MaterialDomains.Add(MaterialDomain.Create(domainId, $"Domain {suffix}", $"D{suffix}"));
        dbContext.MaterialCategories.Add(MaterialCategory.Create(categoryId, domainId, null, $"Category {suffix}", $"C{suffix}"));
        dbContext.MaterialFamilies.Add(MaterialFamily.Create(familyId, categoryId, $"Family {suffix}", $"F{suffix}", unitId));
        dbContext.Materials.Add(Material.Create(materialId, familyId, $"Material {suffix}", $"Material {suffix}", $"M{suffix}", MaterialKind.Consumable, TrackingType.Quantity, false, false, null));
        AddCapability(dbContext, sourceWarehouseId, domainId, OperationType.Transfer);
        if (includeDestinationTransferCapability)
        {
            AddCapability(dbContext, destinationWarehouseId, domainId, OperationType.Transfer);
        }
        await dbContext.SaveChangesAsync();
        return new M5Seed(userId, sourceWarehouseId, destinationWarehouseId, unitId, materialId);
    }

    private async Task CreateAndPostOpeningAsync(M5Seed seed, decimal quantity)
    {
        await CreateAndPostOpeningAsync(seed, seed.SourceWarehouseId, quantity);
    }

    private async Task CreateAndPostOpeningAsync(M5Seed seed, Guid warehouseId, decimal quantity)
    {
        SubmittedDocument opening = await CreateSubmittedDocumentAsync(
            seed,
            warehouseId,
            DocumentType.Opening,
            quantity,
            null,
            OpeningType.Initial);
        (await PostAsync(opening.Id, opening.RowVersion, seed.UserId)).IsSuccess.ShouldBeTrue();
    }

    private Task<SubmittedDocument> CreateSubmittedTransferAsync(M5Seed seed, decimal quantity) =>
        CreateSubmittedTransferAsync(seed, seed.SourceWarehouseId, seed.DestinationWarehouseId, quantity);

    private Task<SubmittedDocument> CreateSubmittedTransferAsync(
        M5Seed seed,
        Guid sourceWarehouseId,
        Guid destinationWarehouseId,
        decimal quantity) =>
        CreateSubmittedDocumentAsync(
            seed,
            sourceWarehouseId,
            DocumentType.Transfer,
            quantity,
            destinationWarehouseId,
            null);

    private async Task<SubmittedDocument> CreateSubmittedReversalAsync(M5Seed seed, Guid sourceDocumentId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        WarehouseDocument sourceDocument = await dbContext.WarehouseDocuments
            .SingleAsync(item => item.Id == sourceDocumentId);
        List<DocumentLine> sourceLines = await dbContext.DocumentLines
            .Where(item => item.DocumentId == sourceDocumentId)
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .ToListAsync();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var reversal = WarehouseDocument.CreateDraft(
            Guid.NewGuid(),
            sourceDocument.WarehouseId,
            sourceDocument.DocumentType,
            $"REV-{suffix}",
            sourceDocument.Id);
        dbContext.WarehouseDocuments.Add(reversal);

        foreach (DocumentLine sourceLine in sourceLines)
        {
            Result<DocumentLine> reversalLine = DocumentLine.Create(
                Guid.NewGuid(),
                reversal.Id,
                sourceLine.MaterialId,
                sourceLine.LineType,
                sourceLine.Quantity,
                sourceLine.UnitId,
                sourceLine.BaseQuantity,
                sourceLine.UnitPrice,
                sourceLine.BatchNumber,
                sourceLine.ExpiryDate,
                sourceLine.OpeningType,
                sourceLine.Id);
            reversalLine.IsSuccess.ShouldBeTrue();
            dbContext.DocumentLines.Add(reversalLine.Value);
        }

        await dbContext.SaveChangesAsync();
        var attachment = DocumentAttachment.Create(
            Guid.NewGuid(), reversal.Id, AttachmentType.SignedOriginal, $"m5/{suffix}.pdf", $"{suffix}.pdf",
            "application/pdf", 1, suffix, seed.UserId, DateTime.UtcNow);
        dbContext.DocumentAttachments.Add(attachment);
        await dbContext.SaveChangesAsync();
        reversal.SetSignedCopy(attachment.Id).IsSuccess.ShouldBeTrue();
        reversal.UpdatePaperReference($"P-{suffix}", 2026).IsSuccess.ShouldBeTrue();
        reversal.Submit().IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync();

        return new SubmittedDocument(reversal.Id, reversal.RowVersion);
    }

    private async Task<SubmittedDocument> CreateSubmittedDocumentAsync(
        M5Seed seed,
        Guid sourceWarehouseId,
        DocumentType type,
        decimal quantity,
        Guid? destinationId,
        OpeningType? openingType)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var document = WarehouseDocument.CreateDraft(Guid.NewGuid(), sourceWarehouseId, type, $"{type}-{suffix}");
        Result<DocumentLine> line = DocumentLine.Create(Guid.NewGuid(), document.Id, seed.MaterialId, DocumentLineType.Normal, quantity, seed.UnitId, quantity, null, null, null, openingType);
        line.IsSuccess.ShouldBeTrue();
        dbContext.WarehouseDocuments.Add(document);
        dbContext.DocumentLines.Add(line.Value);
        if (destinationId is not null)
        {
            Result<TransferInfo> info = TransferInfo.Create(document.Id, destinationId.Value, "Replenishment");
            info.IsSuccess.ShouldBeTrue();
            dbContext.TransferInfos.Add(info.Value);
        }
        await dbContext.SaveChangesAsync();
        dbContext.DocumentAttachments.Add(DocumentAttachment.Create(Guid.NewGuid(), document.Id, AttachmentType.SignedOriginal, $"m5/{suffix}.pdf", $"{suffix}.pdf", "application/pdf", 1, suffix, seed.UserId, DateTime.UtcNow));
        await dbContext.SaveChangesAsync();
        Guid attachmentId = await dbContext.DocumentAttachments.Where(item => item.DocumentId == document.Id).Select(item => item.Id).SingleAsync();
        document.SetSignedCopy(attachmentId).IsSuccess.ShouldBeTrue();
        document.UpdatePaperReference($"P-{suffix}", 2026).IsSuccess.ShouldBeTrue();
        document.Submit().IsSuccess.ShouldBeTrue();
        await dbContext.SaveChangesAsync();
        return new SubmittedDocument(document.Id, document.RowVersion);
    }

    private static void AddCapability(
        ApplicationDbContext dbContext,
        Guid warehouseId,
        Guid domainId,
        params OperationType[] operationTypes)
    {
        var capability = WarehouseCapability.Create(Guid.NewGuid(), warehouseId, domainId);
        dbContext.WarehouseCapabilities.Add(capability);
        dbContext.WarehouseCapabilityOperations.AddRange(operationTypes.Select(operationType =>
            WarehouseCapabilityOperation.Create(Guid.NewGuid(), capability.Id, operationType)));
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

    private async Task StartHardFreezeAsync(Guid warehouseId, Guid userId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        InventoryCount count = InventoryCount.Plan(
            Guid.NewGuid(),
            warehouseId,
            userId,
            InventoryCountType.Surprise,
            InventoryCountScopeType.EntireWarehouse,
            null,
            FreezePolicy.HardFreeze,
            DateTime.UtcNow).Value;
        count.Start(DateTime.UtcNow.AddTicks(1)).IsSuccess.ShouldBeTrue();
        context.InventoryCounts.Add(count);
        await context.SaveChangesAsync();
    }

    private static Task<decimal> GetBalanceAsync(ApplicationDbContext context, Guid warehouseId, Guid materialId) => context.InventoryBalances.Where(item => item.WarehouseId == warehouseId && item.MaterialId == materialId).Select(item => item.Quantity).SingleAsync();

    private sealed record M5Seed(Guid UserId, Guid SourceWarehouseId, Guid DestinationWarehouseId, Guid UnitId, Guid MaterialId);
    private sealed record SubmittedDocument(Guid Id, int RowVersion);
}

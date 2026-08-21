using Application.Abstractions.Authentication;
using Application.Abstractions.Assets;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.WarehouseDocuments;
using Application.InventoryAdjustments.AddLine;
using Application.InventoryAdjustments.CreateDisposal;
using Application.InventoryAdjustments.RemoveLine;
using Application.InventoryAdjustments.UpdateLine;
using Application.UnitTests.Abstractions;
using Domain.Common;
using Domain.Assets;
using Domain.DocumentLines;
using Domain.InventoryAdjustments;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.WarehouseDocuments;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.M7;

public sealed class InventoryAdjustmentMutationTests : BaseHandlerTest
{
    [Fact]
    public void Validators_Should_RejectInvalidAdjustmentMutationCommands()
    {
        // Arrange
        var add = new AddAdjustmentLineCommand(Guid.Empty, Guid.Empty, 0m, null, string.Empty, 0);
        var update = new UpdateAdjustmentLineCommand(Guid.Empty, Guid.Empty, 0m, null, string.Empty, 0);
        var remove = new RemoveAdjustmentLineCommand(Guid.Empty, Guid.Empty, 0);
        var disposal = new CreateDisposalCommand(Guid.Empty, Array.Empty<Guid>(), string.Empty);

        // Act
        TestValidationResult<AddAdjustmentLineCommand> addResult =
            new AddAdjustmentLineCommandValidator().TestValidate(add);
        TestValidationResult<UpdateAdjustmentLineCommand> updateResult =
            new UpdateAdjustmentLineCommandValidator().TestValidate(update);
        TestValidationResult<RemoveAdjustmentLineCommand> removeResult =
            new RemoveAdjustmentLineCommandValidator().TestValidate(remove);
        TestValidationResult<CreateDisposalCommand> disposalResult =
            new CreateDisposalCommandValidator().TestValidate(disposal);

        // Assert
        addResult.Errors.ShouldNotBeEmpty();
        updateResult.Errors.ShouldNotBeEmpty();
        removeResult.Errors.ShouldNotBeEmpty();
        disposalResult.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void Validators_Should_AcceptValidAdjustmentMutationCommands()
    {
        // Arrange
        var add = new AddAdjustmentLineCommand(Guid.NewGuid(), Guid.NewGuid(), -2m, null, "Variance", 1);
        var update = new UpdateAdjustmentLineCommand(Guid.NewGuid(), Guid.NewGuid(), 3m, null, "Correction", 2);
        var remove = new RemoveAdjustmentLineCommand(Guid.NewGuid(), Guid.NewGuid(), 3);
        var disposal = new CreateDisposalCommand(Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid()], "Damaged");

        // Act
        TestValidationResult<AddAdjustmentLineCommand> addResult =
            new AddAdjustmentLineCommandValidator().TestValidate(add);
        TestValidationResult<UpdateAdjustmentLineCommand> updateResult =
            new UpdateAdjustmentLineCommandValidator().TestValidate(update);
        TestValidationResult<RemoveAdjustmentLineCommand> removeResult =
            new RemoveAdjustmentLineCommandValidator().TestValidate(remove);
        TestValidationResult<CreateDisposalCommand> disposalResult =
            new CreateDisposalCommandValidator().TestValidate(disposal);

        // Assert
        addResult.ShouldNotHaveAnyValidationErrors();
        updateResult.ShouldNotHaveAnyValidationErrors();
        removeResult.ShouldNotHaveAnyValidationErrors();
        disposalResult.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task RemoveHandler_Should_DeleteBothLineRecords_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var documentId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var document = WarehouseDocument.CreateDraft(
            documentId, Guid.NewGuid(), DocumentType.Adjustment, "ADJ-1");
        context.WarehouseDocuments.Add(document);
        context.InventoryAdjustments.Add(InventoryAdjustment.Create(
            documentId, null, AdjustmentKind.Quantity, "Variance").Value);
        context.DocumentLines.Add(DocumentLine.Create(
            lineId, documentId, Guid.NewGuid(), DocumentLineType.Normal,
            2m, null, 2m, null, null, null).Value);
        context.AdjustmentLines.Add(AdjustmentLine.Create(
            lineId, documentId, -2m, "Variance").Value);
        await context.SaveChangesAsync();
        var handler = new RemoveAdjustmentLineCommandHandler(
            context, CreateUserContext(), CreateAuthorization(true));

        // Act
        Result result = await handler.Handle(
            new RemoveAdjustmentLineCommand(documentId, lineId, document.RowVersion), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.DocumentLines.AnyAsync(item => item.Id == lineId)).ShouldBeFalse();
        (await context.AdjustmentLines.AnyAsync(item => item.Id == lineId)).ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveHandler_Should_ReturnRowVersionMismatch_WhenStale()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), Guid.NewGuid(), DocumentType.Adjustment, "ADJ-2");
        context.WarehouseDocuments.Add(document);
        await context.SaveChangesAsync();
        var handler = new RemoveAdjustmentLineCommandHandler(
            context, CreateUserContext(), CreateAuthorization(true));

        // Act
        Result result = await handler.Handle(
            new RemoveAdjustmentLineCommand(document.Id, Guid.NewGuid(), document.RowVersion + 1),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("WarehouseDocuments.RowVersionMismatch");
    }

    [Fact]
    public async Task CreateDisposalHandler_Should_CreateOneAtomicLinePerAsset_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var warehouseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var materialId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var firstAssetId = Guid.NewGuid();
        var secondAssetId = Guid.NewGuid();
        context.MaterialFamilies.Add(MaterialFamily.Create(
            familyId, Guid.NewGuid(), "Family", "FAMILY", unitId));
        context.Materials.Add(Material.Create(
            materialId, familyId, "Asset", null, "ASSET", MaterialKind.Asset,
            TrackingType.Serial, false, true, null));
        context.Assets.AddRange(
            Asset.CreateReceived(firstAssetId, materialId, warehouseId, Guid.NewGuid(), "A-1", new DateOnly(2026, 8, 21)).Value,
            Asset.CreateReceived(secondAssetId, materialId, warehouseId, Guid.NewGuid(), "A-2", new DateOnly(2026, 8, 21)).Value);
        context.AssetCurrentStatuses.AddRange(
            new AssetCurrentStatusView
            {
                AssetId = firstAssetId,
                MaterialId = materialId,
                WarehouseId = warehouseId,
                AssetNumber = "A-1",
                CurrentStatus = AssetCurrentStatus.InStock
            },
            new AssetCurrentStatusView
            {
                AssetId = secondAssetId,
                MaterialId = materialId,
                WarehouseId = warehouseId,
                AssetNumber = "A-2",
                CurrentStatus = AssetCurrentStatus.InCustody
            });
        await context.SaveChangesAsync();

        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), warehouseId, DocumentType.Adjustment, "DIS-1");
        IWarehouseDocumentDraftFactory draftFactory = Substitute.For<IWarehouseDocumentDraftFactory>();
        draftFactory.CreateAsync(warehouseId, DocumentType.Adjustment, Arg.Any<CancellationToken>())
            .Returns(document);
        IApplicationTransaction transaction = CreateTransaction();
        IAssetKeyLock assetLock = Substitute.For<IAssetKeyLock>();
        IAssetLifecycleGuard lifecycleGuard = Substitute.For<IAssetLifecycleGuard>();
        lifecycleGuard.EnsureNotDisposedAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        var handler = new CreateDisposalCommandHandler(
            context, CreateUserContext(), CreateAuthorization(true), draftFactory, transaction,
            assetLock, lifecycleGuard, Substitute.For<IDatabaseExceptionClassifier>());

        // Act
        Result<Guid> result = await handler.Handle(
            new CreateDisposalCommand(warehouseId, [firstAssetId, secondAssetId], "Damaged"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.DocumentLines.CountAsync(item => item.DocumentId == document.Id)).ShouldBe(2);
        (await context.AdjustmentLines.CountAsync(item => item.AdjustmentId == document.Id)).ShouldBe(2);
        (await context.DocumentLineAssetSelections.CountAsync(item => item.DocumentId == document.Id)).ShouldBe(2);
        (await context.AdjustmentLines.SingleAsync(item => item.Difference == -1m)).ShouldNotBeNull();
        (await context.AdjustmentLines.SingleAsync(item => item.Difference == 0m)).ShouldNotBeNull();
        await assetLock.Received(1).AcquireAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(ids.OrderBy(id => id))),
            Arg.Any<CancellationToken>());
    }

    private static IUserContext CreateUserContext()
    {
        IUserContext context = Substitute.For<IUserContext>();
        context.UserId.Returns(Guid.NewGuid());
        return context;
    }

    private static IScopeAuthorizationService CreateAuthorization(bool allowed)
    {
        IScopeAuthorizationService service = Substitute.For<IScopeAuthorizationService>();
        service.HasPermissionInScopeAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<ScopeType>(), Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(allowed);
        return service;
    }

    private static IApplicationTransaction CreateTransaction()
    {
        IApplicationTransaction transaction = Substitute.For<IApplicationTransaction>();
        transaction.ExecuteAsync(
                Arg.Any<Func<CancellationToken, Task<Result<Guid>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task<Result<Guid>>>>(0)(
                call.ArgAt<CancellationToken>(1)));
        return transaction;
    }
}

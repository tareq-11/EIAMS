using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Materials;
using Application.Materials.Create;
using Application.Materials.Update;
using Application.UnitTests.Abstractions;
using Domain.Common;
using Domain.DocumentLines;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.StockMovements;
using Domain.UnitsOfMeasure;
using Domain.WarehouseDocuments;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SharedKernel;

namespace Application.UnitTests.Materials;

/// <summary>
/// 3B: server-side classification matrix, the derived (non-writable) asset-number flag, the material
/// catalog version, and the classification lock once a material is operationally used.
/// </summary>
public sealed class MaterialClassificationProvenanceTests : BaseHandlerTest
{
    [Theory]
    [InlineData(MaterialKind.Consumable, TrackingType.Quantity, false)]
    [InlineData(MaterialKind.Durable, TrackingType.Quantity, false)]
    [InlineData(MaterialKind.Durable, TrackingType.Serial, false)]
    [InlineData(MaterialKind.Asset, TrackingType.Serial, true)]
    public void Create_Should_StartAtFirstCatalogVersionAndDeriveAssetNumber(
        MaterialKind materialKind,
        TrackingType trackingType,
        bool expectedRequiresAssetNumber)
    {
        // Arrange
        Material material = CreateMaterial(materialKind, trackingType);

        // Act
        int catalogVersion = material.CatalogVersion;
        bool requiresAssetNumber = material.RequiresAssetNumber;

        // Assert
        catalogVersion.ShouldBe(1);
        requiresAssetNumber.ShouldBe(expectedRequiresAssetNumber);
        requiresAssetNumber.ShouldBe(material.IsAssetTracked);
    }

    [Fact]
    public void UpdateDetails_Should_IncrementCatalogVersion_WhenClassificationChanges()
    {
        // Arrange
        Material material = CreateMaterial(MaterialKind.Durable, TrackingType.Quantity);

        // Act
        material.UpdateDetails("مادة", "Material", MaterialKind.Durable, TrackingType.Serial, false, null);

        // Assert
        material.CatalogVersion.ShouldBe(2);
    }

    [Fact]
    public void UpdateDetails_Should_KeepCatalogVersion_WhenOnlyDescriptiveFieldsChange()
    {
        // Arrange
        Material material = CreateMaterial(MaterialKind.Consumable, TrackingType.Quantity);

        // Act
        material.UpdateDetails("اسم جديد", "New name", MaterialKind.Consumable, TrackingType.Quantity, true, null);

        // Assert
        material.CatalogVersion.ShouldBe(1);
        material.NameAr.ShouldBe("اسم جديد");
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenFamilyIsInactive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        CatalogSeed seed = await SeedCatalogAsync(context);
        seed.Family.SetStatus(Status.Inactive);
        await context.SaveChangesAsync();
        var handler = new CreateMaterialCommandHandler(context, CreateUserContext(), CreateAuthorization(true));
        var command = new CreateMaterialCommand(
            seed.FamilyId, seed.BaseUnitId, "مادة", "Material", "MAT-INACTIVE-FAMILY",
            MaterialKind.Consumable, TrackingType.Quantity, false, null);

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(MaterialErrors.FamilyNotActive);
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenCategoryIsInactive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        CatalogSeed seed = await SeedCatalogAsync(context);
        seed.Category.SetStatus(Status.Inactive);
        await context.SaveChangesAsync();
        var handler = new CreateMaterialCommandHandler(context, CreateUserContext(), CreateAuthorization(true));
        var command = new CreateMaterialCommand(
            seed.FamilyId, seed.BaseUnitId, "مادة", "Material", "MAT-INACTIVE-CATEGORY",
            MaterialKind.Consumable, TrackingType.Quantity, false, null);

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(MaterialErrors.CategoryNotActive);
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenMaterialDomainIsInactive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        CatalogSeed seed = await SeedCatalogAsync(context);
        seed.Domain.SetStatus(Status.Inactive);
        await context.SaveChangesAsync();
        var handler = new CreateMaterialCommandHandler(context, CreateUserContext(), CreateAuthorization(true));
        var command = new CreateMaterialCommand(
            seed.FamilyId, seed.BaseUnitId, "مادة", "Material", "MAT-INACTIVE-DOMAIN",
            MaterialKind.Consumable, TrackingType.Quantity, false, null);

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(MaterialErrors.DomainNotActive);
    }

    [Fact]
    public async Task Handle_Should_Succeed_WhenClassificationChainIsActive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        CatalogSeed seed = await SeedCatalogAsync(context);
        var handler = new CreateMaterialCommandHandler(context, CreateUserContext(), CreateAuthorization(true));
        var command = new CreateMaterialCommand(
            seed.FamilyId, seed.BaseUnitId, "مادة", "Material", "MAT-ACTIVE-CHAIN",
            MaterialKind.Durable, TrackingType.Serial, false, null);

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Materials.FindAsync(result.Value))!.CatalogVersion.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenBaseUnitIsInactive()
    {
        await using TestDbContext context = CreateDbContext();
        CatalogSeed seed = await SeedCatalogAsync(context);
        UnitOfMeasure unit = await context.UnitsOfMeasure.SingleAsync(u => u.Id == seed.BaseUnitId);
        unit.SetStatus(Status.Inactive);
        await context.SaveChangesAsync();
        var handler = new CreateMaterialCommandHandler(context, CreateUserContext(), CreateAuthorization(true));
        var command = new CreateMaterialCommand(
            seed.FamilyId, seed.BaseUnitId, "مادة", "Material", "MAT-INACTIVE-UNIT",
            MaterialKind.Consumable, TrackingType.Quantity, false, null);

        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(MaterialErrors.UnitNotActive(seed.BaseUnitId));
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenClassificationChangesAfterOperationalUse()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        CatalogSeed seed = await SeedCatalogAsync(context);
        Material material = CreateMaterial(MaterialKind.Durable, TrackingType.Quantity);
        context.Materials.Add(material);
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), Guid.NewGuid(), DocumentType.Receiving, "REF-1");
        document.UpdatePaperReference("P-REF-1", 2026);
        document.Submit();
        context.WarehouseDocuments.Add(document);
        context.DocumentLines.Add(DocumentLine.Create(
            Guid.NewGuid(), document.Id, material.Id, DocumentLineType.Normal, 1m, seed.BaseUnitId, 1m,
            null, null, null).Value);
        await context.SaveChangesAsync();
        var handler = new UpdateMaterialCommandHandler(
                context, CreateUserContext(), CreateAuthorization(true), CreateTransaction(), CreateMaterialLock());
        var command = new UpdateMaterialCommand(
            material.Id, "مادة", "Material", MaterialKind.Asset, TrackingType.Serial, false, null,
            material.CatalogVersion);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(MaterialErrors.ClassificationLocked(material.Id));
        (await context.Materials.FindAsync(material.Id))!.MaterialKind.ShouldBe(MaterialKind.Durable);
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenClassificationChangesAfterPostedMovement()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        CatalogSeed seed = await SeedCatalogAsync(context);
        Material material = CreateMaterial(MaterialKind.Consumable, TrackingType.Quantity);
        context.Materials.Add(material);
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), Guid.NewGuid(), DocumentType.Issue, "REF-2");
        context.WarehouseDocuments.Add(document);
        Result<DocumentLine> line = DocumentLine.Create(
            Guid.NewGuid(), document.Id, material.Id, DocumentLineType.Normal, 1m, seed.BaseUnitId, 1m,
            null, null, null);
        context.DocumentLines.Add(line.Value);
        Result<StockMovement> movement = StockMovement.Create(
            Guid.NewGuid(), document.WarehouseId, material.Id, document.Id, line.Value.Id,
            MovementType.Issue, -1m, Guid.NewGuid(), DateTime.UtcNow);
        context.StockMovements.Add(movement.Value);
        await context.SaveChangesAsync();
        var handler = new UpdateMaterialCommandHandler(
                context, CreateUserContext(), CreateAuthorization(true), CreateTransaction(), CreateMaterialLock());
        var command = new UpdateMaterialCommand(
            material.Id, "مادة", "Material", MaterialKind.Asset, TrackingType.Serial, false, null,
            material.CatalogVersion);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(MaterialErrors.ClassificationLocked(material.Id));
    }

    [Fact]
    public async Task Handle_Should_Succeed_WhenOnlyDescriptiveFieldsChangeAfterOperationalUse()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        CatalogSeed seed = await SeedCatalogAsync(context);
        Material material = CreateMaterial(MaterialKind.Asset, TrackingType.Serial);
        context.Materials.Add(material);
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), Guid.NewGuid(), DocumentType.Receiving, "REF-3");
        document.UpdatePaperReference("P-REF-3", 2026);
        document.Submit();
        context.WarehouseDocuments.Add(document);
        context.DocumentLines.Add(DocumentLine.Create(
            Guid.NewGuid(), document.Id, material.Id, DocumentLineType.Asset, 1m, seed.BaseUnitId, 1m,
            null, null, null).Value);
        await context.SaveChangesAsync();
        var handler = new UpdateMaterialCommandHandler(
                context, CreateUserContext(), CreateAuthorization(true), CreateTransaction(), CreateMaterialLock());
        var command = new UpdateMaterialCommand(
            material.Id, "مادة معدلة", "Updated", MaterialKind.Asset, TrackingType.Serial, true, null,
            material.CatalogVersion);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Material stored = await context.Materials.FindAsync(material.Id);
        stored!.NameAr.ShouldBe("مادة معدلة");
        stored.CatalogVersion.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_Succeed_WhenClassificationChangesAndOnlyDraftUsageExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        CatalogSeed seed = await SeedCatalogAsync(context);
        Material material = CreateMaterial(MaterialKind.Durable, TrackingType.Quantity);
        context.Materials.Add(material);
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), Guid.NewGuid(), DocumentType.Receiving, "REF-4");
        context.WarehouseDocuments.Add(document);
        context.DocumentLines.Add(DocumentLine.Create(
            Guid.NewGuid(), document.Id, material.Id, DocumentLineType.Normal, 1m, seed.BaseUnitId, 1m,
            null, null, null).Value);
        await context.SaveChangesAsync();
        var handler = new UpdateMaterialCommandHandler(
                context, CreateUserContext(), CreateAuthorization(true), CreateTransaction(), CreateMaterialLock());
        var command = new UpdateMaterialCommand(
            material.Id, "مادة", "Material", MaterialKind.Durable, TrackingType.Serial, false, null,
            material.CatalogVersion);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Materials.FindAsync(material.Id))!.CatalogVersion.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenTrackingTypeConflictsWithRequestedKind()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Material material = CreateMaterial(MaterialKind.Consumable, TrackingType.Quantity);
        context.Materials.Add(material);
        await context.SaveChangesAsync();
        var handler = new UpdateMaterialCommandHandler(
                context, CreateUserContext(), CreateAuthorization(true), CreateTransaction(), CreateMaterialLock());
        var command = new UpdateMaterialCommand(
            material.Id, "مادة", "Material", MaterialKind.Consumable, TrackingType.Serial, false, null,
            material.CatalogVersion);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(MaterialErrors.ConsumableMustBeQuantityTracked);
    }

    [Theory]
    [InlineData(MaterialKind.Consumable, TrackingType.Serial)]
    [InlineData(MaterialKind.Asset, TrackingType.Quantity)]
    public void Validators_Should_RejectClassificationMatrixViolations(
        MaterialKind materialKind,
        TrackingType trackingType)
    {
        // Arrange
        var createCommand = new CreateMaterialCommand(
            Guid.NewGuid(), Guid.NewGuid(), "مادة", null, "MAT-1", materialKind, trackingType, false, null);
        var updateCommand = new UpdateMaterialCommand(
            Guid.NewGuid(), "مادة", null, materialKind, trackingType, false, null, 1);

        // Act
        ValidationResult createValidation = new CreateMaterialCommandValidator().Validate(createCommand);
        ValidationResult updateValidation = new UpdateMaterialCommandValidator().Validate(updateCommand);

        // Assert
        createValidation.IsValid.ShouldBeFalse();
        updateValidation.IsValid.ShouldBeFalse();
    }

    private static Material CreateMaterial(MaterialKind materialKind, TrackingType trackingType) =>
        Material.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "مادة",
            "Material",
            $"MAT-{Guid.NewGuid():N}",
            materialKind,
            trackingType,
            false,
            null);

    private static async Task<CatalogSeed> SeedCatalogAsync(TestDbContext context)
    {
        var domainId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var baseUnitId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var domain = MaterialDomain.Create(domainId, "Domain", "DOM");
        var category = MaterialCategory.Create(categoryId, domainId, null, "Category", "CAT");
        var family = MaterialFamily.Create(familyId, categoryId, "Family", "FAM");
        context.MaterialDomains.Add(domain);
        context.MaterialCategories.Add(category);
        context.UnitsOfMeasure.Add(UnitOfMeasure.Create(baseUnitId, "Piece", "pc", "Count"));
        context.MaterialFamilies.Add(family);
        await context.SaveChangesAsync();

        return new CatalogSeed(domain, category, family, familyId, baseUnitId);
    }

    /// <summary>Pass-through transaction: the in-memory context has no transaction to begin.</summary>
    private static IApplicationTransaction CreateTransaction()
    {
        IApplicationTransaction transaction = Substitute.For<IApplicationTransaction>();
        transaction.ExecuteAsync(Arg.Any<Func<CancellationToken, Task<Result>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task<Result>>>(0)(
                call.ArgAt<CancellationToken>(1)));
        return transaction;
    }

    private static IMaterialOperationLock CreateMaterialLock()
    {
        IMaterialOperationLock materialLock = Substitute.For<IMaterialOperationLock>();
        materialLock.AcquireAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return materialLock;
    }

    private static IUserContext CreateUserContext()
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        return userContext;
    }

    private static IScopeAuthorizationService CreateAuthorization(bool authorized)
    {
        IScopeAuthorizationService authorization = Substitute.For<IScopeAuthorizationService>();
        authorization.HasPermissionInScopeAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<ScopeType>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(authorized));
        return authorization;
    }

    private sealed record CatalogSeed(
        MaterialDomain Domain,
        MaterialCategory Category,
        MaterialFamily Family,
        Guid FamilyId,
        Guid BaseUnitId);
}

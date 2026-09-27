using Application.Abstractions.Assets;
using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Materials;
using Application.Abstractions.Posting;
using Application.DocumentLines;
using Application.DocumentLines.Add;
using Application.DocumentLines.Update;
using Application.UnitTests.Abstractions;
using Application.WarehouseDocuments.Submit;
using Domain.Common;
using Domain.DocumentLines;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.MaterialUnitConversions;
using Domain.UnitsOfMeasure;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using SharedKernel;

namespace Application.UnitTests.DocumentLines;

/// <summary>
/// 3B draft-stage provenance: a document line freezes the material catalog version, classification,
/// base unit and conversion factor it was written against, and submit/post re-validates that
/// snapshot so a later catalog edit cannot reinterpret the document.
/// </summary>
public sealed class DocumentLineProvenanceTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_CaptureSourceMaterialVersionAndBaseUnit_WhenLineIsAddedInBaseUnit()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        CatalogSeed seed = await SeedCatalogAsync(context);
        var handler = new AddDocumentLineCommandHandler(
            context, CreateUserContext(), CreateAuthorization(true), AssetOptions());

        // Act
        Result<Guid> result = await handler.Handle(
            new AddDocumentLineCommand(
                seed.DocumentId, seed.MaterialId, 5m, seed.BaseUnitId, null, null, null, null, seed.RowVersion),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        DocumentLine line = await context.DocumentLines.FindAsync(result.Value);
        line!.Provenance.ShouldBe(new DocumentLineProvenance(
            1,
            MaterialKind.Consumable,
            TrackingType.Quantity,
            seed.BaseUnitId));
        line.BaseQuantity.ShouldBe(5m);
    }

    [Fact]
    public async Task Handle_Should_CaptureConversionProvenance_WhenLineIsAddedInANonBaseUnit()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        CatalogSeed seed = await SeedCatalogAsync(context);
        var conversion = MaterialUnitConversion.Create(
            Guid.NewGuid(), seed.MaterialId, seed.SourceUnitId, seed.BaseUnitId, 12m);
        context.MaterialUnitConversions.Add(conversion);
        await context.SaveChangesAsync();
        var handler = new AddDocumentLineCommandHandler(
            context, CreateUserContext(), CreateAuthorization(true), AssetOptions());

        // Act
        Result<Guid> result = await handler.Handle(
            new AddDocumentLineCommand(
                seed.DocumentId, seed.MaterialId, 2m, seed.SourceUnitId, null, null, null, null, seed.RowVersion),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        DocumentLine line = await context.DocumentLines.FindAsync(result.Value);
        line!.Provenance.ShouldBe(new DocumentLineProvenance(
            1,
            MaterialKind.Consumable,
            TrackingType.Quantity,
            seed.BaseUnitId,
            conversion.Id,
            seed.SourceUnitId,
            seed.BaseUnitId,
            12m));
        line.BaseQuantity.ShouldBe(24m);
    }

    [Fact]
    public async Task Handle_Should_ReCaptureProvenance_WhenCatalogVersionChangedBeforeLineUpdate()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        CatalogSeed seed = await SeedCatalogAsync(context);
        var addHandler = new AddDocumentLineCommandHandler(
            context, CreateUserContext(), CreateAuthorization(true), AssetOptions());
        Result<Guid> added = await addHandler.Handle(
            new AddDocumentLineCommand(
                seed.DocumentId, seed.MaterialId, 5m, seed.BaseUnitId, null, null, null, null, seed.RowVersion),
            CancellationToken.None);
        seed.Material.UpdateDetails(
            "مادة", "Material", MaterialKind.Durable, TrackingType.Serial, false, null);
        await context.SaveChangesAsync();
        var updateHandler = new UpdateDocumentLineCommandHandler(
            context, CreateUserContext(), CreateAuthorization(true), AssetOptions());
        WarehouseDocument document = await context.WarehouseDocuments.FindAsync(seed.DocumentId);
        int rowVersion = document!.RowVersion;

        // Act
        Result result = await updateHandler.Handle(
            new UpdateDocumentLineCommand(
                seed.DocumentId, added.Value, 5m, seed.BaseUnitId, null, null, null, null, rowVersion),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        DocumentLine line = await context.DocumentLines.FindAsync(added.Value);
        line!.Provenance!.MaterialVersion.ShouldBe(2);
        line.Provenance.MaterialKind.ShouldBe(MaterialKind.Durable);
    }

    [Fact]
    public async Task Handle_Should_FailSubmit_WhenMaterialClassificationChangedAfterDraft()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        CatalogSeed seed = await SeedCatalogAsync(context);
        int rowVersion = await AddLineAsync(context, seed, seed.BaseUnitId, 5m);
        seed.Material.UpdateDetails(
            "مادة", "Material", MaterialKind.Asset, TrackingType.Serial, false, null);
        await context.SaveChangesAsync();
        var handler = new SubmitDocumentCommandHandler(
            context, CreateUserContext(), CreateAuthorization(true), CreateTransaction(),
            CreateMaterialLock(), AssetOptions(), []);

        // Act
        Result result = await handler.Handle(
            new SubmitDocumentCommand(seed.DocumentId, rowVersion), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("DocumentLines.MaterialProvenanceStale");
        (await context.WarehouseDocuments.FindAsync(seed.DocumentId))!.DocumentStatus
            .ShouldBe(DocumentStatus.Draft);
    }

    [Fact]
    public async Task Handle_Should_FailSubmit_WhenConversionFactorChangedAfterDraft()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        CatalogSeed seed = await SeedCatalogAsync(context);
        var conversion = MaterialUnitConversion.Create(
            Guid.NewGuid(), seed.MaterialId, seed.SourceUnitId, seed.BaseUnitId, 12m);
        context.MaterialUnitConversions.Add(conversion);
        await context.SaveChangesAsync();
        int rowVersion = await AddLineAsync(context, seed, seed.SourceUnitId, 2m);
        conversion.UpdateFactor(15m);
        await context.SaveChangesAsync();
        var handler = new SubmitDocumentCommandHandler(
            context, CreateUserContext(), CreateAuthorization(true), CreateTransaction(),
            CreateMaterialLock(), AssetOptions(), []);

        // Act
        Result result = await handler.Handle(
            new SubmitDocumentCommand(seed.DocumentId, rowVersion), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("DocumentLines.ConversionProvenanceChanged");
    }

    [Fact]
    public async Task Handle_Should_FailSubmit_WhenConversionRemovedAfterDraft()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        CatalogSeed seed = await SeedCatalogAsync(context);
        var conversion = MaterialUnitConversion.Create(
            Guid.NewGuid(), seed.MaterialId, seed.SourceUnitId, seed.BaseUnitId, 12m);
        context.MaterialUnitConversions.Add(conversion);
        await context.SaveChangesAsync();
        int rowVersion = await AddLineAsync(context, seed, seed.SourceUnitId, 2m);
        context.MaterialUnitConversions.Remove(conversion);
        await context.SaveChangesAsync();
        var handler = new SubmitDocumentCommandHandler(
            context, CreateUserContext(), CreateAuthorization(true), CreateTransaction(),
            CreateMaterialLock(), AssetOptions(), []);

        // Act
        Result result = await handler.Handle(
            new SubmitDocumentCommand(seed.DocumentId, rowVersion), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("DocumentLines.UnitConversionNotFound");
    }

    [Fact]
    public async Task Handle_Should_SucceedSubmit_WhenCatalogIsUnchangedSinceDraft()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        CatalogSeed seed = await SeedCatalogAsync(context);
        int rowVersion = await AddLineAsync(context, seed, seed.BaseUnitId, 5m);
        var handler = new SubmitDocumentCommandHandler(
            context, CreateUserContext(), CreateAuthorization(true), CreateTransaction(),
            CreateMaterialLock(), AssetOptions(), []);

        // Act
        Result result = await handler.Handle(
            new SubmitDocumentCommand(seed.DocumentId, rowVersion), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.WarehouseDocuments.FindAsync(seed.DocumentId))!.DocumentStatus
            .ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void Validate_Should_Succeed_WhenCatalogIsUnchanged()
    {
        // Arrange
        Material material = CreateMaterial();
        DocumentLine line = CreateLine(new DocumentLineProvenance(
            material.CatalogVersion, material.MaterialKind, material.TrackingType, material.BaseUnitId));
        MaterialUnitConversion? conversion = null;

        // Act
        Result result = DocumentLineProvenanceRules.Validate(Guid.NewGuid(), line, material, conversion);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Should_Fail_WhenMaterialVersionChanged()
    {
        // Arrange
        Material material = CreateMaterial();
        DocumentLine line = CreateLine(new DocumentLineProvenance(
            material.CatalogVersion, material.MaterialKind, material.TrackingType, material.BaseUnitId));
        material.UpdateDetails("مادة", "Material", MaterialKind.Durable, TrackingType.Serial, false, null);
        var documentId = Guid.NewGuid();

        // Act
        Result result = DocumentLineProvenanceRules.Validate(documentId, line, material, null);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DocumentLineErrors.MaterialProvenanceStale(documentId, line.Id, material.Id, 1, 2));
    }

    [Fact]
    public void Validate_Should_Fail_WhenBaseUnitChanged()
    {
        // Arrange
        Material material = CreateMaterial();
        DocumentLine line = CreateLine(new DocumentLineProvenance(
            material.CatalogVersion, material.MaterialKind, material.TrackingType, material.BaseUnitId));
        var documentId = Guid.NewGuid();
        var newBaseUnitId = Guid.NewGuid();

        // Act
        Result result = DocumentLineProvenanceRules.Validate(
            documentId, line, MoveToBaseUnit(material, newBaseUnitId), null);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DocumentLineErrors.BaseUnitProvenanceStale(
            documentId, line.Id, material.Id, material.BaseUnitId, newBaseUnitId));
    }

    [Fact]
    public void Validate_Should_Fail_WhenConversionFactorChanged()
    {
        // Arrange
        Material material = CreateMaterial();
        var fromUnitId = Guid.NewGuid();
        var conversionId = Guid.NewGuid();
        DocumentLine line = CreateLine(new DocumentLineProvenance(
            material.CatalogVersion,
            material.MaterialKind,
            material.TrackingType,
            material.BaseUnitId,
            conversionId,
            fromUnitId,
            material.BaseUnitId,
            12m));
        var current = MaterialUnitConversion.Create(
            conversionId, material.Id, fromUnitId, material.BaseUnitId, 15m);
        var documentId = Guid.NewGuid();

        // Act
        Result result = DocumentLineProvenanceRules.Validate(documentId, line, material, current);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DocumentLineErrors.ConversionProvenanceChanged(
            documentId, line.Id, conversionId, 12m, 15m));
    }

    [Fact]
    public void Validate_Should_Fail_WhenConversionDisappeared()
    {
        // Arrange
        Material material = CreateMaterial();
        var fromUnitId = Guid.NewGuid();
        var conversionId = Guid.NewGuid();
        DocumentLine line = CreateLine(new DocumentLineProvenance(
            material.CatalogVersion,
            material.MaterialKind,
            material.TrackingType,
            material.BaseUnitId,
            conversionId,
            fromUnitId,
            material.BaseUnitId,
            12m));
        var documentId = Guid.NewGuid();

        // Act
        Result result = DocumentLineProvenanceRules.Validate(documentId, line, material, null);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DocumentLineErrors.ConversionProvenanceStale(
            documentId, line.Id, material.Id, conversionId, null));
    }

    [Fact]
    public void Validate_Should_Fail_WhenLineHasNoCapturedProvenance()
    {
        // Arrange
        // A line created before provenance capture: the live catalog matches the line's quantity and
        // line type, yet the revision it was written against was never recorded, so it is unverifiable.
        Material material = CreateMaterial();
        DocumentLine line = CreateLine(null);
        var documentId = Guid.NewGuid();

        // Act
        Result result = DocumentLineProvenanceRules.Validate(
            documentId, line, material, MaterialUnitConversion.Create(
                Guid.NewGuid(), material.Id, Guid.NewGuid(), material.BaseUnitId, 3m));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DocumentLineErrors.ProvenanceNotCaptured(line.DocumentId, line.Id));
    }

    private static Material CreateMaterial() => Material.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        "مادة",
        "Material",
        $"MAT-{Guid.NewGuid():N}",
        MaterialKind.Consumable,
        TrackingType.Quantity,
        false,
        null);

    private static Material MoveToBaseUnit(Material material, Guid baseUnitId)
    {
        // The base unit has no write command yet, so the check is exercised through a second
        // material that shares the classification but not the base unit.
        return Material.Create(
            material.Id,
            material.FamilyId,
            baseUnitId,
            material.NameAr,
            material.NameEn,
            material.Code,
            material.MaterialKind,
            material.TrackingType,
            material.HasExpiry,
            material.Attributes);
    }

    private static DocumentLine CreateLine(DocumentLineProvenance? provenance) => DocumentLine.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        DocumentLineType.Normal,
        1m,
        provenance?.BaseUnitId,
        1m,
        null,
        null,
        null,
        provenance: provenance).Value;

    /// <summary>Adds the draft line and returns the document row version the add produced.</summary>
    private static async Task<int> AddLineAsync(
        TestDbContext context,
        CatalogSeed seed,
        Guid unitId,
        decimal quantity)
    {
        var handler = new AddDocumentLineCommandHandler(
            context, CreateUserContext(), CreateAuthorization(true), AssetOptions());
        Result<Guid> result = await handler.Handle(
            new AddDocumentLineCommand(
                seed.DocumentId, seed.MaterialId, quantity, unitId, null, null, null, null, seed.RowVersion),
            CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        WarehouseDocument document = await context.WarehouseDocuments.FindAsync(seed.DocumentId);
        return document!.RowVersion;
    }

    private static IOptions<AssetCreationOptions> AssetOptions() =>
        Options.Create(new AssetCreationOptions());

    private static async Task<CatalogSeed> SeedCatalogAsync(TestDbContext context)
    {
        var domainId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var baseUnitId = Guid.NewGuid();
        var sourceUnitId = Guid.NewGuid();
        var materialId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        context.MaterialDomains.Add(MaterialDomain.Create(domainId, "Domain", "DOM"));
        context.MaterialCategories.Add(MaterialCategory.Create(categoryId, domainId, null, "Category", "CAT"));
        context.UnitsOfMeasure.Add(UnitOfMeasure.Create(baseUnitId, "Piece", "pc", "Count"));
        context.UnitsOfMeasure.Add(UnitOfMeasure.Create(sourceUnitId, "Box", "box", "Count"));
        context.MaterialFamilies.Add(MaterialFamily.Create(familyId, categoryId, "Family", "FAM"));
        var material = Material.Create(
            materialId, familyId, baseUnitId, "مادة", "Material", $"MAT-{materialId:N}",
            MaterialKind.Consumable, TrackingType.Quantity, false, null);
        context.Materials.Add(material);
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), warehouseId, DocumentType.Receiving, "REF-1");
        document.UpdatePaperReference("P-REF-1", 2026);
        context.WarehouseDocuments.Add(document);
        await context.SaveChangesAsync();

        return new CatalogSeed(
            material,
            document,
            document.Id,
            materialId,
            baseUnitId,
            sourceUnitId,
            document.RowVersion);
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
        Material Material,
        WarehouseDocument Document,
        Guid DocumentId,
        Guid MaterialId,
        Guid BaseUnitId,
        Guid SourceUnitId,
        int RowVersion);
}

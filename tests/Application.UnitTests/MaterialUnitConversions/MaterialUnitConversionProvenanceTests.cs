using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.DocumentLines;
using Application.MaterialUnitConversions.Remove;
using Application.MaterialUnitConversions.Update;
using Application.UnitTests.Abstractions;
using Domain.Common;
using Domain.DocumentLines;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.MaterialUnitConversions;
using Domain.StockMovements;
using Domain.UnitsOfMeasure;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SharedKernel;

namespace Application.UnitTests.MaterialUnitConversions;

/// <summary>
/// 3B: a conversion whose factor is part of an operational document's provenance may no longer be
/// re-factored or removed. Draft-only usage stays mutable because a draft line is re-captured by
/// editing it, and is re-validated at submit.
/// </summary>
public sealed class MaterialUnitConversionProvenanceTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_Fail_WhenProvenanceIsUsedBySubmittedDocument()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        ConversionSeed seed = await SeedConversionAsync(context, DocumentStatus.Submitted);
        var handler = new UpdateMaterialUnitConversionCommandHandler(
            context, CreateUserContext(), CreateAuthorization(true));

        // Act
        Result result = await handler.Handle(
            new UpdateMaterialUnitConversionCommand(seed.MaterialId, seed.ConversionId, 99m),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(MaterialUnitConversionErrors.ProvenanceInUse(seed.ConversionId));
        (await context.MaterialUnitConversions.FindAsync(seed.ConversionId))!.Factor.ShouldBe(10m);
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenProvenanceIsUsedByPostedMovement()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        ConversionSeed seed = await SeedConversionAsync(context, DocumentStatus.Draft);
        Result<StockMovement> movement = StockMovement.Create(
            Guid.NewGuid(), Guid.NewGuid(), seed.MaterialId, seed.DocumentId, seed.LineId,
            MovementType.Receipt, 10m, Guid.NewGuid(), DateTime.UtcNow);
        context.StockMovements.Add(movement.Value);
        await context.SaveChangesAsync();
        var handler = new RemoveMaterialUnitConversionCommandHandler(
            context, CreateUserContext(), CreateAuthorization(true));

        // Act
        Result result = await handler.Handle(
            new RemoveMaterialUnitConversionCommand(seed.MaterialId, seed.ConversionId),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(MaterialUnitConversionErrors.ProvenanceInUse(seed.ConversionId));
        (await context.MaterialUnitConversions.FindAsync(seed.ConversionId)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Handle_Should_Succeed_WhenProvenanceIsOnlyUsedByDraftLine()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        ConversionSeed seed = await SeedConversionAsync(context, DocumentStatus.Draft);
        var handler = new UpdateMaterialUnitConversionCommandHandler(
            context, CreateUserContext(), CreateAuthorization(true));

        // Act
        Result result = await handler.Handle(
            new UpdateMaterialUnitConversionCommand(seed.MaterialId, seed.ConversionId, 42m),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.MaterialUnitConversions.FindAsync(seed.ConversionId))!.Factor.ShouldBe(42m);
    }

    [Fact]
    public async Task Handle_Should_Succeed_WhenConversionIsNotUsedByAnyOperationalDocument()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        ConversionSeed seed = await SeedConversionAsync(context, DocumentStatus.Draft);
        var handler = new RemoveMaterialUnitConversionCommandHandler(
            context, CreateUserContext(), CreateAuthorization(true));

        // Act
        Result result = await handler.Handle(
            new RemoveMaterialUnitConversionCommand(seed.MaterialId, seed.ConversionId),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.MaterialUnitConversions.FindAsync(seed.ConversionId)).ShouldBeNull();
    }

    private static async Task<ConversionSeed> SeedConversionAsync(
        TestDbContext context,
        DocumentStatus documentStatus)
    {
        var domainId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var baseUnitId = Guid.NewGuid();
        var sourceUnitId = Guid.NewGuid();
        var materialId = Guid.NewGuid();
        context.MaterialDomains.Add(MaterialDomain.Create(domainId, "Domain", "DOM"));
        context.MaterialCategories.Add(MaterialCategory.Create(categoryId, domainId, null, "Category", "CAT"));
        context.UnitsOfMeasure.Add(UnitOfMeasure.Create(baseUnitId, "Piece", "pc", "Count"));
        context.UnitsOfMeasure.Add(UnitOfMeasure.Create(sourceUnitId, "Box", "box", "Count"));
        context.MaterialFamilies.Add(MaterialFamily.Create(familyId, categoryId, "Family", "FAM"));
        context.Materials.Add(Material.Create(
            materialId, familyId, baseUnitId, "مادة", "Material", $"MAT-{materialId:N}",
            MaterialKind.Consumable, TrackingType.Quantity, false, null));
        var conversion = MaterialUnitConversion.Create(
            Guid.NewGuid(), materialId, sourceUnitId, baseUnitId, 10m);
        context.MaterialUnitConversions.Add(conversion);
        await context.SaveChangesAsync();

        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), Guid.NewGuid(), DocumentType.Receiving, $"REF-{documentStatus}");
        if (documentStatus != DocumentStatus.Draft)
        {
            document.UpdatePaperReference("P-REF", 2026);
            document.Submit();
        }

        context.WarehouseDocuments.Add(document);
        var lineId = Guid.NewGuid();
        context.DocumentLines.Add(DocumentLine.Create(
            lineId,
            document.Id,
            materialId,
            DocumentLineType.Normal,
            1m,
            sourceUnitId,
            10m,
            null,
            null,
            null,
            provenance: new DocumentLineProvenance(
                1,
                MaterialKind.Consumable,
                TrackingType.Quantity,
                baseUnitId,
                conversion.Id,
                sourceUnitId,
                baseUnitId,
                10m)).Value);
        await context.SaveChangesAsync();

        return new ConversionSeed(materialId, conversion.Id, document.Id, lineId);
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

    private sealed record ConversionSeed(Guid MaterialId, Guid ConversionId, Guid DocumentId, Guid LineId);
}

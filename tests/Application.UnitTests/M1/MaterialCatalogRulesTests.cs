using Application.DocumentLines;
using Domain.Common;
using Domain.MaterialCategories;
using Domain.Materials;
using Domain.MaterialUnitConversions;
using Domain.UnitsOfMeasure;
using SharedKernel;

namespace Application.UnitTests.M1;

public sealed class MaterialCatalogRulesTests
{
    [Fact]
    public void UnitOfMeasure_UpdateDetails_Should_PreserveIdAndRaiseEvent()
    {
        var id = Guid.NewGuid();
        var unit = UnitOfMeasure.Create(id, "Piece", "pc", "Count");
        unit.ClearDomainEvents();

        unit.UpdateDetails("Box", "box", "Packaging");

        unit.Id.ShouldBe(id);
        unit.Name.ShouldBe("Box");
        unit.Symbol.ShouldBe("box");
        unit.UnitType.ShouldBe("Packaging");
        unit.DomainEvents.ShouldContain(domainEvent => domainEvent is UnitOfMeasureUpdatedDomainEvent);
    }

    [Fact]
    public void MaterialCategory_MoveTo_Should_ChangeParentAndRaiseEvent()
    {
        var category = MaterialCategory.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "Computers",
            "COMPUTERS");
        var parentId = Guid.NewGuid();
        category.ClearDomainEvents();

        category.MoveTo(parentId);

        category.ParentCategoryId.ShouldBe(parentId);
        category.DomainEvents.ShouldContain(domainEvent => domainEvent is MaterialCategoryMovedDomainEvent);
    }

    [Fact]
    public void MaterialCategory_MoveTo_Should_IgnoreSameParent()
    {
        var parentId = Guid.NewGuid();
        var category = MaterialCategory.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            parentId,
            "Computers",
            "COMPUTERS");
        category.ClearDomainEvents();

        category.MoveTo(parentId);

        category.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Material_SetStatus_Should_TreatArchivedAsTerminal()
    {
        Material material = CreateMaterial(MaterialKind.Consumable, false);
        material.SetStatus(MaterialStatus.Archived).IsSuccess.ShouldBeTrue();
        material.ClearDomainEvents();

        Result result = material.SetStatus(MaterialStatus.Active);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(MaterialErrors.ArchivedIsTerminal);
        material.Status.ShouldBe(MaterialStatus.Archived);
        material.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Material_SetStatus_Should_IgnoreNoOpChange()
    {
        Material material = CreateMaterial(MaterialKind.Consumable, false);
        material.ClearDomainEvents();

        Result result = material.SetStatus(MaterialStatus.Active);

        result.IsSuccess.ShouldBeTrue();
        material.DomainEvents.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(MaterialKind.Asset, false, true)]
    [InlineData(MaterialKind.Consumable, true, true)]
    [InlineData(MaterialKind.Consumable, false, false)]
    public void Material_IsAssetTracked_Should_UseKindOrAssetNumberRequirement(
        MaterialKind materialKind,
        bool requiresAssetNumber,
        bool expected)
    {
        Material material = CreateMaterial(materialKind, requiresAssetNumber);

        material.IsAssetTracked.ShouldBe(expected);
    }

    [Fact]
    public void MaterialUnitConversion_Create_Should_PreserveFactorAndRaiseEvent()
    {
        var conversion = MaterialUnitConversion.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            12.5m);

        conversion.Factor.ShouldBe(12.5m);
        conversion.DomainEvents.ShouldContain(
            domainEvent => domainEvent is MaterialUnitConversionCreatedDomainEvent);
    }

    [Fact]
    public void BaseQuantityCalculator_Should_ReturnQuantityForBaseUnit()
    {
        var baseUnitId = Guid.NewGuid();

        Result<decimal> result = BaseQuantityCalculator.Calculate(
            Guid.NewGuid(),
            7.25m,
            baseUnitId,
            baseUnitId,
            null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(7.25m);
    }

    [Fact]
    public void BaseQuantityCalculator_Should_ApplyConversionAndRoundToThreeDecimals()
    {
        var materialId = Guid.NewGuid();
        var sourceUnitId = Guid.NewGuid();
        var baseUnitId = Guid.NewGuid();
        var conversion = MaterialUnitConversion.Create(
            Guid.NewGuid(),
            materialId,
            sourceUnitId,
            baseUnitId,
            1.23456m);

        Result<decimal> result = BaseQuantityCalculator.Calculate(
            materialId,
            2m,
            sourceUnitId,
            baseUnitId,
            conversion);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(2.469m);
    }

    [Fact]
    public void BaseQuantityCalculator_Should_FailWhenConversionIsMissing()
    {
        Result<decimal> result = BaseQuantityCalculator.Calculate(
            Guid.NewGuid(),
            2m,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("DocumentLines.UnitConversionNotFound");
    }

    [Theory]
    [InlineData("1.0001")]
    [InlineData("1000000000000000")]
    public void BaseQuantityCalculator_Should_RejectUnsupportedQuantityPrecisionOrMagnitude(
        string quantityValue)
    {
        decimal quantity = decimal.Parse(quantityValue, System.Globalization.CultureInfo.InvariantCulture);

        Result<decimal> result = BaseQuantityCalculator.Calculate(
            Guid.NewGuid(),
            quantity,
            null,
            Guid.NewGuid(),
            null);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(Domain.DocumentLines.DocumentLineErrors.QuantityPrecisionInvalid);
    }

    private static Material CreateMaterial(MaterialKind materialKind, bool requiresAssetNumber) =>
        Material.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "مادة",
            "Material",
            $"MAT-{Guid.NewGuid():N}",
            materialKind,
            TrackingType.Quantity,
            false,
            requiresAssetNumber,
            null);
}

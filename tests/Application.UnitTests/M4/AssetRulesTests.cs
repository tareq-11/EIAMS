using Application.Abstractions.Assets;
using Application.DocumentLines;
using Domain.Assets;
using Domain.Common;
using Domain.Materials;
using SharedKernel;

namespace Application.UnitTests.M4;

public sealed class AssetRulesTests
{
    [Fact]
    public void IsAssetTracked_Should_BeTrue_WhenMaterialKindIsAsset()
    {
        Material material = CreateMaterial(MaterialKind.Asset, requiresAssetNumber: false);

        material.IsAssetTracked.ShouldBeTrue();
    }

    [Fact]
    public void IsAssetTracked_Should_BeTrue_WhenAssetNumberIsRequired()
    {
        Material material = CreateMaterial(MaterialKind.Durable, requiresAssetNumber: true);

        material.IsAssetTracked.ShouldBeTrue();
    }

    [Fact]
    public void AssetLineRules_Should_RejectFractionalBaseQuantity()
    {
        Result result = AssetLineRules.Validate(Guid.NewGuid(), DocumentLineType.Asset, 1.5m, 100);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("DocumentLines.AssetQuantityMustBeWhole");
    }

    [Fact]
    public void AssetLineRules_Should_RejectQuantityAboveConfiguredLimit()
    {
        Result result = AssetLineRules.Validate(Guid.NewGuid(), DocumentLineType.Asset, 101m, 100);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("DocumentLines.AssetQuantityLimitExceeded");
    }

    [Fact]
    public void DocumentAssetLimitRules_Should_RejectTooManyLines()
    {
        var options = new AssetCreationOptions
        {
            MaxAssetsPerLine = 100,
            MaxAssetsPerDocument = 500,
            MaxLinesPerDocument = 10
        };

        Result result = DocumentAssetLimitRules.Validate(Guid.NewGuid(), 11, 20m, options);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("DocumentLines.LinesLimitExceeded");
    }

    [Fact]
    public void DocumentAssetLimitRules_Should_RejectTotalAssetsAboveDocumentLimit()
    {
        var options = new AssetCreationOptions
        {
            MaxAssetsPerLine = 100,
            MaxAssetsPerDocument = 500,
            MaxLinesPerDocument = 10
        };

        Result result = DocumentAssetLimitRules.Validate(Guid.NewGuid(), 6, 501m, options);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("DocumentLines.AssetDocumentLimitExceeded");
    }

    [Fact]
    public void DocumentAssetLimitRules_Should_AcceptValuesAtConfiguredLimits()
    {
        var options = new AssetCreationOptions
        {
            MaxAssetsPerLine = 100,
            MaxAssetsPerDocument = 500,
            MaxLinesPerDocument = 10
        };

        Result result = DocumentAssetLimitRules.Validate(Guid.NewGuid(), 10, 500m, options);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CreateReceived_Should_NotStoreStatus_AndShouldRaiseCreatedEvent()
    {
        Result<Asset> result = Asset.CreateReceived(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "AST-001",
            new DateOnly(2026, 8, 13));

        result.IsSuccess.ShouldBeTrue();
        result.Value.RowVersion.ShouldBe(1);
        result.Value.DomainEvents.ShouldContain(domainEvent => domainEvent is AssetCreatedDomainEvent);
        typeof(Asset).GetProperty("Status").ShouldBeNull();
    }

    [Fact]
    public void CreateReceived_Should_RejectWarrantyBeforeAcquisition()
    {
        Result<Asset> result = Asset.CreateReceived(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "AST-002",
            new DateOnly(2026, 8, 13),
            warrantyExpiry: new DateOnly(2026, 8, 12));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AssetErrors.WarrantyBeforeAcquisition);
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
            hasExpiry: false,
            requiresAssetNumber,
            attributes: null);
}

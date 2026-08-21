using Domain.Common;
using Domain.Custodies;
using Domain.InventoryAdjustments;
using Domain.InventoryCounts;
using SharedKernel;

namespace Application.UnitTests.M7;

public sealed class M7DomainRulesTests
{
    [Fact]
    public void InventoryCount_Should_CompleteLifecycle_AndIncrementVersion()
    {
        // Arrange
        DateTime plannedAt = DateTime.UtcNow;
        InventoryCount count = InventoryCount.Plan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            InventoryCountType.Scheduled, InventoryCountScopeType.EntireWarehouse,
            null, FreezePolicy.HardFreeze, plannedAt).Value;

        // Act
        Result start = count.Start(plannedAt.AddMinutes(1));
        Result complete = count.Complete(plannedAt.AddMinutes(2));
        Result close = count.Close(plannedAt.AddMinutes(3));

        // Assert
        start.IsSuccess.ShouldBeTrue();
        complete.IsSuccess.ShouldBeTrue();
        close.IsSuccess.ShouldBeTrue();
        count.Status.ShouldBe(InventoryCountStatus.Closed);
        count.RowVersion.ShouldBe(4);
    }

    [Fact]
    public void InventoryCount_Should_RejectInvalidScopeReference()
    {
        // Arrange

        // Act
        Result<InventoryCount> result = InventoryCount.Plan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            InventoryCountType.Cycle, InventoryCountScopeType.MaterialDomain,
            null, FreezePolicy.NoFreeze, DateTime.UtcNow);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(InventoryCountErrors.ScopeReferenceInvalid);
    }

    [Fact]
    public void InventoryCount_Should_RejectSkippedTransition()
    {
        // Arrange
        InventoryCount count = InventoryCount.Plan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            InventoryCountType.Surprise, InventoryCountScopeType.EntireWarehouse,
            null, FreezePolicy.SoftFreeze, DateTime.UtcNow).Value;

        // Act
        Result result = count.Complete(DateTime.UtcNow.AddMinutes(1));

        // Assert
        result.IsFailure.ShouldBeTrue();
        count.Status.ShouldBe(InventoryCountStatus.Planned);
    }

    [Fact]
    public void InventoryCountLine_Should_ComputeSignedDifference()
    {
        // Arrange
        InventoryCountLine line = InventoryCountLine.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 10m).Value;

        // Act
        Result result = line.RecordActual(7.5m);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        line.ActualQuantity.ShouldBe(7.5m);
        line.Difference.ShouldBe(-2.5m);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void InventoryCountLine_Should_RejectInvalidAssetActual(decimal actual)
    {
        // Arrange
        InventoryCountLine line = InventoryCountLine.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1m).Value;

        // Act
        Result result = line.RecordActual(actual);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void InventoryCountLine_Should_RequireReasonForVariance()
    {
        // Arrange
        InventoryCountLine line = InventoryCountLine.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 4m).Value;
        line.RecordActual(3m);

        // Act
        bool before = line.HasRequiredVarianceReason();
        Result update = line.SetVarianceReason("  damaged during storage  ");

        // Assert
        before.ShouldBeFalse();
        update.IsSuccess.ShouldBeTrue();
        line.HasRequiredVarianceReason().ShouldBeTrue();
        line.VarianceReason.ShouldBe("damaged during storage");
    }

    [Fact]
    public void QuantityAdjustment_Should_PostAndReverse()
    {
        // Arrange
        InventoryAdjustment adjustment = InventoryAdjustment.Create(
            Guid.NewGuid(), null, AdjustmentKind.Quantity, "Physical count variance").Value;

        // Act
        Result post = adjustment.MarkPosted();
        Result reverse = adjustment.MarkReversed();

        // Assert
        post.IsSuccess.ShouldBeTrue();
        reverse.IsSuccess.ShouldBeTrue();
        adjustment.Status.ShouldBe(InventoryAdjustmentStatus.Reversed);
    }

    [Fact]
    public void DisposalAdjustment_Should_RejectReversal()
    {
        // Arrange
        InventoryAdjustment adjustment = InventoryAdjustment.Create(
            Guid.NewGuid(), null, AdjustmentKind.Disposal, "Unserviceable asset").Value;
        adjustment.MarkPosted();

        // Act
        Result result = adjustment.MarkReversed();

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Disposals.ReversalNotAllowed");
    }

    [Fact]
    public void AdjustmentLine_Should_RequireSignedNonZeroDifference_ForQuantityAdjustment()
    {
        // Arrange

        // Act
        Result<AdjustmentLine> result = AdjustmentLine.Create(
            Guid.NewGuid(), Guid.NewGuid(), 0m, "No variance");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AdjustmentLineErrors.ZeroDifference);
    }

    [Fact]
    public void Custody_Should_CloseForDisposal_WithoutReturnReference()
    {
        // Arrange
        DateTime fromUtc = DateTime.UtcNow.AddMinutes(-2);
        Custody custody = Custody.Open(Guid.NewGuid(), Guid.NewGuid(), PartyType.Employee,
            Guid.NewGuid(), CustodyKind.Personal, Guid.NewGuid(), fromUtc).Value;
        var disposalDocumentId = Guid.NewGuid();

        // Act
        Result result = custody.CloseForDisposal(disposalDocumentId, fromUtc.AddMinutes(1));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        custody.Status.ShouldBe(CustodyStatus.Closed);
        custody.ReturnDocumentId.ShouldBeNull();
        custody.DisposalDocumentId.ShouldBe(disposalDocumentId);
    }
}

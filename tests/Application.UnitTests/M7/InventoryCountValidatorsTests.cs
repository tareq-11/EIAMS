using Application.InventoryCounts.Plan;
using Application.InventoryCounts.RecordActualBatch;
using Domain.Common;
using FluentValidation.TestHelper;

namespace Application.UnitTests.M7;

public sealed class InventoryCountValidatorsTests
{
    [Fact]
    public void PlanValidator_Should_AcceptEachValidScopeShape()
    {
        // Arrange
        var validator = new PlanInventoryCountCommandValidator();
        var warehouseId = Guid.NewGuid();
        var selectedMaterialId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        PlanInventoryCountCommand[] commands =
        [
            new(warehouseId, InventoryCountType.Scheduled, InventoryCountScopeType.EntireWarehouse,
                null, [], FreezePolicy.NoFreeze),
            new(warehouseId, InventoryCountType.Cycle, InventoryCountScopeType.MaterialDomain,
                domainId, [], FreezePolicy.SoftFreeze),
            new(warehouseId, InventoryCountType.Surprise, InventoryCountScopeType.SelectedMaterials,
                null, [selectedMaterialId], FreezePolicy.HardFreeze)
        ];

        // Act
        TestValidationResult<PlanInventoryCountCommand>[] results = commands
            .Select(command => validator.TestValidate(command))
            .ToArray();

        // Assert
        results.ShouldAllBe(result => result.IsValid);
    }

    [Fact]
    public void PlanValidator_Should_RejectFieldsThatDoNotMatchScope()
    {
        // Arrange
        var validator = new PlanInventoryCountCommandValidator();
        var command = new PlanInventoryCountCommand(
            Guid.NewGuid(),
            InventoryCountType.Cycle,
            InventoryCountScopeType.EntireWarehouse,
            Guid.NewGuid(),
            [Guid.NewGuid()],
            FreezePolicy.NoFreeze);

        // Act
        TestValidationResult<PlanInventoryCountCommand> result = validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.MaterialDomainId);
        result.ShouldHaveValidationErrorFor(item => item.MaterialIds);
    }

    [Fact]
    public void PlanValidator_Should_RejectEmptySelectedMaterials()
    {
        // Arrange
        var validator = new PlanInventoryCountCommandValidator();
        var command = new PlanInventoryCountCommand(
            Guid.NewGuid(), InventoryCountType.Cycle, InventoryCountScopeType.SelectedMaterials,
            null, [], FreezePolicy.NoFreeze);

        // Act
        TestValidationResult<PlanInventoryCountCommand> result = validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.MaterialIds);
    }

    [Fact]
    public void ActualsValidator_Should_RejectDuplicateAndOversizedBatches()
    {
        // Arrange
        var validator = new RecordInventoryCountActualsCommandValidator();
        var lineId = Guid.NewGuid();
        InventoryCountActualInput[] actuals = Enumerable.Range(0, 101)
            .Select(index => new InventoryCountActualInput(index < 2 ? lineId : Guid.NewGuid(), 1m))
            .ToArray();
        var command = new RecordInventoryCountActualsCommand(Guid.NewGuid(), actuals, 1);

        // Act
        TestValidationResult<RecordInventoryCountActualsCommand> result = validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Actuals);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1.0001)]
    public void ActualsValidator_Should_RejectInvalidQuantity(decimal quantity)
    {
        // Arrange
        var validator = new RecordInventoryCountActualsCommandValidator();
        var command = new RecordInventoryCountActualsCommand(
            Guid.NewGuid(), [new InventoryCountActualInput(Guid.NewGuid(), quantity)], 1);

        // Act
        TestValidationResult<RecordInventoryCountActualsCommand> result = validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Actuals[0].ActualQuantity");
    }
}

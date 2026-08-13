using Domain.Common;
using Domain.WarehouseCapabilities;
using Domain.WarehouseCapabilityOperations;
using Domain.WarehouseMaterialSettings;
using Domain.Warehouses;
using SharedKernel;

namespace Application.UnitTests.M2;

public sealed class WarehouseConfigurationRulesTests
{
    [Fact]
    public void Warehouse_Create_Should_StartActiveWithFirstRowVersion()
    {
        var warehouse = Warehouse.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Main Warehouse",
            "WH-01",
            "General",
            true);

        warehouse.Status.ShouldBe(Status.Active);
        warehouse.RowVersion.ShouldBe(1);
        warehouse.DomainEvents.ShouldContain(domainEvent => domainEvent is WarehouseCreatedDomainEvent);
    }

    [Fact]
    public void Warehouse_UpdateDetails_Should_IncrementRowVersion()
    {
        Warehouse warehouse = CreateWarehouse();
        warehouse.ClearDomainEvents();

        warehouse.UpdateDetails("Updated Warehouse", "Secure", false);

        warehouse.RowVersion.ShouldBe(2);
        warehouse.Name.ShouldBe("Updated Warehouse");
        warehouse.CanHoldStock.ShouldBeFalse();
        warehouse.DomainEvents.ShouldContain(domainEvent => domainEvent is WarehouseUpdatedDomainEvent);
    }

    [Fact]
    public void Warehouse_SetStatus_Should_NotIncrementRowVersionForNoOp()
    {
        Warehouse warehouse = CreateWarehouse();
        warehouse.ClearDomainEvents();

        warehouse.SetStatus(Status.Active);

        warehouse.RowVersion.ShouldBe(1);
        warehouse.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void WarehouseCapability_SetStatus_Should_RaiseEventOnlyForRealChange()
    {
        var capability = WarehouseCapability.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());
        capability.ClearDomainEvents();

        capability.SetStatus(Status.Active);
        capability.DomainEvents.ShouldBeEmpty();

        capability.SetStatus(Status.Inactive);
        capability.Status.ShouldBe(Status.Inactive);
        capability.DomainEvents.ShouldContain(
            domainEvent => domainEvent is WarehouseCapabilityStatusChangedDomainEvent);
    }

    [Fact]
    public void WarehouseCapabilityOperation_MarkAsRemoved_Should_RaiseRemovedEvent()
    {
        var operation = WarehouseCapabilityOperation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            OperationType.Receiving);
        operation.ClearDomainEvents();

        operation.MarkAsRemoved();

        operation.DomainEvents.ShouldContain(
            domainEvent => domainEvent is WarehouseCapabilityOperationRemovedDomainEvent);
    }

    [Fact]
    public void WarehouseMaterialSetting_Create_Should_AcceptValidRange()
    {
        Result<WarehouseMaterialSetting> result = WarehouseMaterialSetting.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            5m,
            20m);

        result.IsSuccess.ShouldBeTrue();
        result.Value.MinQuantity.ShouldBe(5m);
        result.Value.MaxQuantity.ShouldBe(20m);
        result.Value.Status.ShouldBe(Status.Active);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(1, -10)]
    [InlineData(11, 10)]
    public void WarehouseMaterialSetting_Create_Should_RejectInvalidRange(
        int minQuantity,
        int maxQuantity)
    {
        Result<WarehouseMaterialSetting> result = WarehouseMaterialSetting.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            minQuantity,
            maxQuantity);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("WarehouseMaterialSettings.InvalidRange");
    }

    [Fact]
    public void WarehouseMaterialSetting_UpdateThresholds_Should_NotMutateOnFailure()
    {
        WarehouseMaterialSetting setting = WarehouseMaterialSetting.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            5m,
            20m).Value;
        setting.ClearDomainEvents();

        Result result = setting.UpdateThresholds(30m, 10m);

        result.IsFailure.ShouldBeTrue();
        setting.MinQuantity.ShouldBe(5m);
        setting.MaxQuantity.ShouldBe(20m);
        setting.DomainEvents.ShouldBeEmpty();
    }

    private static Warehouse CreateWarehouse() =>
        Warehouse.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Main Warehouse",
            "WH-01",
            "General",
            true);
}

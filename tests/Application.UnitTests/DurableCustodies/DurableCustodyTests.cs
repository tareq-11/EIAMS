using Domain.Common;
using Domain.DurableCustodyAllocations;
using Domain.TrackedMaterialUnits;
using SharedKernel;

namespace Application.UnitTests.DurableCustodies;

public sealed class DurableCustodyTests
{
    [Fact]
    public void DurableCustodyAllocation_Open_Should_Succeed_WithValidInputs()
    {
        // Arrange & Act
        Result<DurableCustodyAllocation> result = DurableCustodyAllocation.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            PartyType.Employee,
            Guid.NewGuid(),
            CustodyKind.Personal,
            Guid.NewGuid(),
            10.5m,
            DateTime.UtcNow);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        DurableCustodyAllocation allocation = result.Value;
        allocation.IssuedQuantity.ShouldBe(10.5m);
        allocation.ActiveQuantity.ShouldBe(10.5m);
        allocation.ReturnedQuantity.ShouldBe(0m);
        allocation.Status.ShouldBe(DurableCustodyAllocationStatus.Active);
        allocation.DomainEvents.ShouldContain(e => e is DurableCustodyAllocationOpenedDomainEvent);
    }

    [Fact]
    public void DurableCustodyAllocation_Open_Should_Fail_WhenQuantityIsZeroOrNegative()
    {
        Result<DurableCustodyAllocation> result = DurableCustodyAllocation.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            PartyType.Employee,
            Guid.NewGuid(),
            CustodyKind.Personal,
            Guid.NewGuid(),
            0m,
            DateTime.UtcNow);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DurableCustodyAllocationErrors.InvalidQuantity);
    }

    [Fact]
    public void DurableCustodyAllocation_Open_Should_Fail_WhenPersonalCustodyAssignedToNonEmployee()
    {
        Result<DurableCustodyAllocation> result = DurableCustodyAllocation.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            PartyType.Site,
            Guid.NewGuid(),
            CustodyKind.Personal,
            Guid.NewGuid(),
            5m,
            DateTime.UtcNow);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DurableCustodyAllocationErrors.PersonalRequiresEmployee);
    }

    [Fact]
    public void DurableCustodyAllocation_Open_Should_Fail_WhenOperationalCustodyAssignedToEmployee()
    {
        Result<DurableCustodyAllocation> result = DurableCustodyAllocation.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            PartyType.Employee,
            Guid.NewGuid(),
            CustodyKind.Operational,
            Guid.NewGuid(),
            5m,
            DateTime.UtcNow);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DurableCustodyAllocationErrors.OperationalRequiresNonEmployee);
    }

    [Fact]
    public void DurableCustodyAllocation_RecordReturn_Should_PartiallyReturn_Correctly()
    {
        // Arrange
        DurableCustodyAllocation allocation = DurableCustodyAllocation.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            PartyType.Employee,
            Guid.NewGuid(),
            CustodyKind.Personal,
            Guid.NewGuid(),
            10m,
            DateTime.UtcNow).Value;

        // Act
        Result result = allocation.RecordReturn(4m, Guid.NewGuid(), DateTime.UtcNow);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        allocation.ActiveQuantity.ShouldBe(6m);
        allocation.ReturnedQuantity.ShouldBe(4m);
        allocation.IssuedQuantity.ShouldBe(10m);
        allocation.Status.ShouldBe(DurableCustodyAllocationStatus.Active);
    }

    [Fact]
    public void DurableCustodyAllocation_RecordReturn_Should_FullyReturn_WhenActiveReachesZero()
    {
        // Arrange
        DurableCustodyAllocation allocation = DurableCustodyAllocation.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            PartyType.Employee,
            Guid.NewGuid(),
            CustodyKind.Personal,
            Guid.NewGuid(),
            10m,
            DateTime.UtcNow).Value;

        // Act
        Result result = allocation.RecordReturn(10m, Guid.NewGuid(), DateTime.UtcNow);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        allocation.ActiveQuantity.ShouldBe(0m);
        allocation.ReturnedQuantity.ShouldBe(10m);
        allocation.Status.ShouldBe(DurableCustodyAllocationStatus.FullyReturned);
    }

    [Fact]
    public void DurableCustodyAllocation_RecordReturn_Should_Fail_WhenReturnExceedsActive()
    {
        // Arrange
        DurableCustodyAllocation allocation = DurableCustodyAllocation.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            PartyType.Employee,
            Guid.NewGuid(),
            CustodyKind.Personal,
            Guid.NewGuid(),
            10m,
            DateTime.UtcNow).Value;

        // Act
        Result result = allocation.RecordReturn(15m, Guid.NewGuid(), DateTime.UtcNow);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DurableCustodyAllocationErrors.ReturnQuantityExceedsActive(15m, 10m));
    }

    [Fact]
    public void TrackedMaterialUnit_Issue_Should_Succeed_WithValidInputs()
    {
        // Arrange & Act
        Result<TrackedMaterialUnit> result = TrackedMaterialUnit.Issue(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "SN-12345",
            Guid.NewGuid(),
            PartyType.Employee,
            Guid.NewGuid(),
            CustodyKind.Personal,
            Guid.NewGuid(),
            DateTime.UtcNow);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        TrackedMaterialUnit unit = result.Value;
        unit.SerialNumber.ShouldBe("SN-12345");
        unit.Status.ShouldBe(TrackedMaterialUnitStatus.Issued);
        unit.DomainEvents.ShouldContain(e => e is TrackedMaterialUnitIssuedDomainEvent);
    }

    [Fact]
    public void TrackedMaterialUnit_Return_Should_Succeed_WhenIssued()
    {
        // Arrange
        TrackedMaterialUnit unit = TrackedMaterialUnit.Issue(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "SN-12345",
            Guid.NewGuid(),
            PartyType.Employee,
            Guid.NewGuid(),
            CustodyKind.Personal,
            Guid.NewGuid(),
            DateTime.UtcNow).Value;

        var returnDocId = Guid.NewGuid();
        DateTime returnedAt = DateTime.UtcNow;

        // Act
        Result result = unit.Return(returnDocId, returnedAt);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        unit.Status.ShouldBe(TrackedMaterialUnitStatus.Returned);
        unit.ReturnDocumentId.ShouldBe(returnDocId);
        unit.ToUtc.ShouldBe(returnedAt);
        unit.DomainEvents.ShouldContain(e => e is TrackedMaterialUnitReturnedDomainEvent);
    }

    [Fact]
    public void TrackedMaterialUnit_Transfer_Should_Succeed_WhenActive()
    {
        // Arrange
        TrackedMaterialUnit unit = TrackedMaterialUnit.Issue(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "SN-12345",
            Guid.NewGuid(),
            PartyType.Employee,
            Guid.NewGuid(),
            CustodyKind.Personal,
            Guid.NewGuid(),
            DateTime.UtcNow).Value;

        var newHolderId = Guid.NewGuid();
        int initialRowVersion = unit.RowVersion;

        // Act
        Result result = unit.Transfer(PartyType.Site, newHolderId, CustodyKind.Operational, DateTime.UtcNow);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        unit.HolderType.ShouldBe(PartyType.Site);
        unit.HolderId.ShouldBe(newHolderId);
        unit.CustodyKind.ShouldBe(CustodyKind.Operational);
        unit.RowVersion.ShouldBe(initialRowVersion + 1);
        unit.DomainEvents.ShouldContain(e => e is TrackedMaterialUnitTransferredDomainEvent);
    }
}

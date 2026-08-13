using Domain.Common;
using Domain.IssueTos;
using Domain.TransferInfos;
using SharedKernel;

namespace Application.UnitTests.M5;

public sealed class IssueAndTransferRulesTests
{
    [Fact]
    public void IssueTo_Should_TrimReasonAndRaiseCreatedEvent_WhenValid()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        // Act
        Result<IssueTo> result = IssueTo.Create(documentId, PartyType.Employee, recipientId, "  Maintenance  ");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.IssueReason.ShouldBe("Maintenance");
        result.Value.DomainEvents.ShouldContain(domainEvent => domainEvent is IssueToCreatedDomainEvent);
    }

    [Fact]
    public void IssueTo_Should_RejectEmptyRecipient_WhenCreated()
    {
        // Arrange
        var documentId = Guid.NewGuid();

        // Act
        Result<IssueTo> result = IssueTo.Create(documentId, PartyType.Employee, Guid.Empty, "Maintenance");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(IssueToErrors.RecipientRequired);
    }

    [Fact]
    public void IssueTo_Should_RejectBlankReason_WhenCreated()
    {
        // Arrange
        var documentId = Guid.NewGuid();

        // Act
        Result<IssueTo> result = IssueTo.Create(documentId, PartyType.Employee, Guid.NewGuid(), "   ");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(IssueToErrors.IssueReasonInvalid);
    }

    [Fact]
    public void TransferInfo_Should_TrimReasonAndRaiseCreatedEvent_WhenValid()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var destinationWarehouseId = Guid.NewGuid();

        // Act
        Result<TransferInfo> result = TransferInfo.Create(documentId, destinationWarehouseId, "  Replenishment  ");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TransferReason.ShouldBe("Replenishment");
        result.Value.DomainEvents.ShouldContain(domainEvent => domainEvent is TransferInfoCreatedDomainEvent);
    }

    [Fact]
    public void TransferInfo_Should_RejectEmptyDestination_WhenCreated()
    {
        // Arrange
        var documentId = Guid.NewGuid();

        // Act
        Result<TransferInfo> result = TransferInfo.Create(documentId, Guid.Empty, "Replenishment");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TransferInfoErrors.DestinationRequired);
    }

    [Fact]
    public void TransferInfo_Should_RejectBlankReason_WhenCreated()
    {
        // Arrange
        var documentId = Guid.NewGuid();

        // Act
        Result<TransferInfo> result = TransferInfo.Create(documentId, Guid.NewGuid(), "   ");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TransferInfoErrors.TransferReasonInvalid);
    }
}

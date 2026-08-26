using Domain.AuditLogs;
using Infrastructure.AuditLogs;

namespace Application.UnitTests.M8;

public sealed class AuditTransitionResolverTests
{
    [Theory]
    [InlineData("Draft", "Submitted", "Submit")]
    [InlineData("Submitted", "Posted", "Post")]
    [InlineData("Posted", "Reversed", "Reverse")]
    [InlineData("Submitted", "Rejected", "Reject")]
    [InlineData("Draft", "Cancelled", "Cancel")]
    [InlineData("Submitted", "Cancelled", "Cancel")]
    [InlineData("Rejected", "Cancelled", "Cancel")]
    [InlineData("Rejected", "Draft", "ReturnToDraft")]
    public void Resolve_Should_MapDocumentStatusTransitionsToWorkflowAction_WhenDocumentStatusChanged(
        string fromStatus,
        string toStatus,
        string expectedAction)
    {
        // Act
        string? action = AuditTransitionResolver.Resolve(
            "WarehouseDocument",
            "document_status",
            fromStatus,
            toStatus);

        // Assert
        action.ShouldBe(expectedAction);
        AuditActions.All.Contains(action!).ShouldBeTrue();
    }

    [Theory]
    [InlineData("Planned", "InProgress", "Start")]
    [InlineData("InProgress", "Completed", "Complete")]
    [InlineData("Completed", "Closed", "Close")]
    public void Resolve_Should_MapCountStatusTransitionsToWorkflowAction_WhenCountStatusChanged(
        string fromStatus,
        string toStatus,
        string expectedAction)
    {
        // Act
        string? action = AuditTransitionResolver.Resolve(
            "InventoryCount",
            "status",
            fromStatus,
            toStatus);

        // Assert
        action.ShouldBe(expectedAction);
        AuditActions.All.Contains(action!).ShouldBeTrue();
    }

    [Theory]
    [InlineData("WarehouseDocument", "document_status", "Posted", "Posted")]
    [InlineData("InventoryCount", "status", "Planned", "Planned")]
    public void Resolve_Should_ReturnNull_WhenStatusDidNotTrulyChange(
        string entityType,
        string field,
        string originalValue,
        string currentValue)
    {
        // Act
        string? action = AuditTransitionResolver.Resolve(entityType, field, originalValue, currentValue);

        // Assert
        action.ShouldBeNull();
    }

    [Fact]
    public void Resolve_Should_ReturnNull_WhenEntityIsUnrelated()
    {
        // Act
        string? action = AuditTransitionResolver.Resolve("Organization", "status", "Active", "Inactive");

        // Assert
        action.ShouldBeNull();
    }

    [Fact]
    public void Resolve_Should_ReturnNull_WhenFieldIsNotAStatusColumn()
    {
        // Act
        string? action = AuditTransitionResolver.Resolve(
            "WarehouseDocument",
            "paper_document_number",
            "old-ref",
            "new-ref");

        // Assert
        action.ShouldBeNull();
    }

    [Fact]
    public void Resolve_Should_ReturnNull_WhenTransitionIsNotMapped()
    {
        // Act
        string? action = AuditTransitionResolver.Resolve(
            "WarehouseDocument",
            "document_status",
            "Posted",
            "Cancelled");

        // Assert
        action.ShouldBeNull();
    }

    [Theory]
    [InlineData(null, "Submitted")]
    [InlineData("Draft", null)]
    public void Resolve_Should_ReturnNull_WhenEitherSideIsNull(string? originalValue, string? currentValue)
    {
        // Act
        string? action = AuditTransitionResolver.Resolve(
            "WarehouseDocument",
            "document_status",
            originalValue,
            currentValue);

        // Assert
        action.ShouldBeNull();
    }
}

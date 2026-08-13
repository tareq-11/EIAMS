using Domain.Common;
using Domain.DocumentLines;
using Domain.InventoryBalances;
using Domain.StockMovements;
using Domain.WarehouseDocuments;
using SharedKernel;

namespace Application.UnitTests.M3;

public sealed class DocumentAndLedgerRulesTests
{
    [Fact]
    public void WarehouseDocument_CreateDraft_Should_InitializeWorkflowAndConcurrencyToken()
    {
        WarehouseDocument document = CreateDraft();

        document.DocumentStatus.ShouldBe(DocumentStatus.Draft);
        document.RowVersion.ShouldBe(1);
        document.DomainEvents.ShouldContain(
            domainEvent => domainEvent is WarehouseDocumentCreatedDomainEvent);
    }

    [Fact]
    public void WarehouseDocument_Submit_Should_RequirePaperReference()
    {
        WarehouseDocument document = CreateDraft();

        Result result = document.Submit();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("WarehouseDocuments.PaperReferenceRequired");
        document.DocumentStatus.ShouldBe(DocumentStatus.Draft);
        document.RowVersion.ShouldBe(1);
    }

    [Fact]
    public void WarehouseDocument_Submit_Should_AdvanceStateAndRowVersion()
    {
        WarehouseDocument document = CreateDraft();
        document.UpdatePaperReference("PAPER-1", 2026).IsSuccess.ShouldBeTrue();
        int versionBeforeSubmit = document.RowVersion;

        Result result = document.Submit();

        result.IsSuccess.ShouldBeTrue();
        document.DocumentStatus.ShouldBe(DocumentStatus.Submitted);
        document.RowVersion.ShouldBe(versionBeforeSubmit + 1);
        document.DomainEvents.ShouldContain(
            domainEvent => domainEvent is WarehouseDocumentSubmittedDomainEvent);
    }

    [Fact]
    public void WarehouseDocument_Post_Should_RequireSignedCopy()
    {
        WarehouseDocument document = CreateSubmittedDocument(includeSignedCopy: false);

        Result result = document.MarkPosted(Guid.NewGuid(), DateTime.UtcNow);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("WarehouseDocuments.SignedCopyRequired");
        document.DocumentStatus.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void WarehouseDocument_Post_Should_RecordActorTimeAndMakeDocumentImmutable()
    {
        var actorId = Guid.NewGuid();
        var postedAtUtc = new DateTime(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc);
        WarehouseDocument document = CreateSubmittedDocument(includeSignedCopy: true);

        Result postResult = document.MarkPosted(actorId, postedAtUtc);
        Result editResult = document.UpdatePaperReference("CHANGED", 2027);

        postResult.IsSuccess.ShouldBeTrue();
        document.DocumentStatus.ShouldBe(DocumentStatus.Posted);
        document.PostedBy.ShouldBe(actorId);
        document.PostedAtUtc.ShouldBe(postedAtUtc);
        editResult.IsFailure.ShouldBeTrue();
        editResult.Error.Code.ShouldBe("WarehouseDocuments.NotEditable");
    }

    [Fact]
    public void WarehouseDocument_RejectedDocument_Should_ReturnToDraft()
    {
        WarehouseDocument document = CreateSubmittedDocument(includeSignedCopy: false);

        document.Reject().IsSuccess.ShouldBeTrue();
        Result result = document.ReturnToDraft();

        result.IsSuccess.ShouldBeTrue();
        document.DocumentStatus.ShouldBe(DocumentStatus.Draft);
        document.DomainEvents.ShouldContain(
            domainEvent => domainEvent is WarehouseDocumentReturnedToDraftDomainEvent);
    }

    [Fact]
    public void WarehouseDocument_Cancel_Should_AllowOnlyUnpostedWorkflowStates()
    {
        WarehouseDocument draft = CreateDraft();
        Result draftCancelResult = draft.Cancel();

        draftCancelResult.IsSuccess.ShouldBeTrue();
        draft.DocumentStatus.ShouldBe(DocumentStatus.Cancelled);

        WarehouseDocument posted = CreateSubmittedDocument(includeSignedCopy: true);
        posted.MarkPosted(Guid.NewGuid(), DateTime.UtcNow).IsSuccess.ShouldBeTrue();
        int postedVersion = posted.RowVersion;

        Result postedCancelResult = posted.Cancel();

        postedCancelResult.IsFailure.ShouldBeTrue();
        postedCancelResult.Error.Code.ShouldBe("WarehouseDocuments.InvalidTransition");
        posted.DocumentStatus.ShouldBe(DocumentStatus.Posted);
        posted.RowVersion.ShouldBe(postedVersion);
    }

    [Fact]
    public void WarehouseDocument_DetailMutation_Should_OnlyIncrementVersionInDraft()
    {
        WarehouseDocument document = CreateDraft();

        document.RegisterDetailMutation().IsSuccess.ShouldBeTrue();
        document.RowVersion.ShouldBe(2);

        document.UpdatePaperReference("PAPER-1", 2026).IsSuccess.ShouldBeTrue();
        document.Submit().IsSuccess.ShouldBeTrue();
        int submittedVersion = document.RowVersion;

        Result result = document.RegisterDetailMutation();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("WarehouseDocuments.NotEditable");
        document.RowVersion.ShouldBe(submittedVersion);
    }

    [Fact]
    public void WarehouseDocument_Reverse_Should_OnlyAllowPostedDocument()
    {
        WarehouseDocument draft = CreateDraft();

        Result invalidResult = draft.MarkReversed();

        invalidResult.IsFailure.ShouldBeTrue();
        invalidResult.Error.Code.ShouldBe("WarehouseDocuments.InvalidTransition");

        WarehouseDocument posted = CreateSubmittedDocument(includeSignedCopy: true);
        posted.MarkPosted(Guid.NewGuid(), DateTime.UtcNow).IsSuccess.ShouldBeTrue();

        posted.MarkReversed().IsSuccess.ShouldBeTrue();
        posted.DocumentStatus.ShouldBe(DocumentStatus.Reversed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DocumentLine_Create_Should_RejectNonPositiveQuantity(int quantity)
    {
        Result<DocumentLine> result = DocumentLine.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DocumentLineType.Normal,
            quantity,
            null,
            1m,
            null,
            null,
            null);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DocumentLineErrors.QuantityMustBePositive);
    }

    [Fact]
    public void DocumentLine_Create_Should_RejectNegativeUnitPrice()
    {
        Result<DocumentLine> result = DocumentLine.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DocumentLineType.Normal,
            1m,
            null,
            1m,
            -0.01m,
            null,
            null);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DocumentLineErrors.UnitPriceMustBeNonNegative);
    }

    [Theory]
    [InlineData(1.0001)]
    [InlineData(999999999999999.9999)]
    public void DocumentLine_Create_Should_RejectQuantityOutsideSupportedPrecision(decimal quantity)
    {
        Result<DocumentLine> result = DocumentLine.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DocumentLineType.Normal,
            quantity,
            null,
            1m,
            null,
            null,
            null);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DocumentLineErrors.QuantityPrecisionInvalid);
    }

    [Fact]
    public void StockMovement_Create_Should_RejectZeroDelta()
    {
        Result<StockMovement> result = StockMovement.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            MovementType.Receipt,
            0m,
            Guid.NewGuid(),
            DateTime.UtcNow);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(StockMovementErrors.DeltaMustNotBeZero);
    }

    [Fact]
    public void InventoryBalance_SetQuantity_Should_RejectNegativeBalanceWithoutMutation()
    {
        var createdAtUtc = new DateTime(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc);
        var balance = InventoryBalance.CreateZero(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            createdAtUtc);
        balance.ClearDomainEvents();

        Result result = balance.SetQuantity(-1m, createdAtUtc.AddMinutes(1));

        result.IsFailure.ShouldBeTrue();
        balance.Quantity.ShouldBe(0m);
        balance.RowVersion.ShouldBe(1);
        balance.LastUpdatedUtc.ShouldBe(createdAtUtc);
        balance.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void InventoryBalance_SetQuantity_Should_AdvanceVersionAndRaiseEventOnValidMutation()
    {
        var createdAtUtc = new DateTime(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc);
        var balance = InventoryBalance.CreateZero(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            createdAtUtc);
        balance.ClearDomainEvents();

        Result result = balance.SetQuantity(12.5m, createdAtUtc.AddMinutes(1));

        result.IsSuccess.ShouldBeTrue();
        balance.Quantity.ShouldBe(12.5m);
        balance.RowVersion.ShouldBe(2);
        balance.DomainEvents.ShouldContain(domainEvent => domainEvent is InventoryBalanceUpdatedDomainEvent);
    }

    private static WarehouseDocument CreateDraft() =>
        WarehouseDocument.CreateDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DocumentType.Receiving,
            $"REC-2026-{Guid.NewGuid():N}");

    private static WarehouseDocument CreateSubmittedDocument(bool includeSignedCopy)
    {
        WarehouseDocument document = CreateDraft();
        document.UpdatePaperReference("PAPER-1", 2026).IsSuccess.ShouldBeTrue();

        if (includeSignedCopy)
        {
            document.SetSignedCopy(Guid.NewGuid()).IsSuccess.ShouldBeTrue();
        }

        document.Submit().IsSuccess.ShouldBeTrue();

        return document;
    }
}

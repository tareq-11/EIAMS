using Domain.Common;
using Domain.Custodies;
using Domain.InventoryCounts;
using Domain.WarehouseDocuments;
using SharedKernel;

namespace IntegrationTests.Regression;

public sealed class StateMachineRegressionTests
{
    [Fact]
    public void WarehouseDocument_LegalTransitions_Should_Succeed()
    {
        // 1. Create Draft
        var doc = WarehouseDocument.CreateDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DocumentType.Receiving,
            "SYS-1");

        doc.DocumentStatus.ShouldBe(DocumentStatus.Draft);

        // 2. Draft -> Submitted
        doc.UpdatePaperReference("PAPER-1", DateTime.UtcNow.Year).IsSuccess.ShouldBeTrue();
        doc.SetSignedCopy(Guid.NewGuid());
        Result submitResult = doc.Submit();
        submitResult.IsSuccess.ShouldBeTrue();
        doc.DocumentStatus.ShouldBe(DocumentStatus.Submitted);

        // 3. Submitted -> Rejected -> Draft
        Result rejectResult = doc.Reject();
        rejectResult.IsSuccess.ShouldBeTrue();
        doc.DocumentStatus.ShouldBe(DocumentStatus.Rejected);

        Result returnToDraftResult = doc.ReturnToDraft();
        returnToDraftResult.IsSuccess.ShouldBeTrue();
        doc.DocumentStatus.ShouldBe(DocumentStatus.Draft);

        // 4. Draft -> Cancelled
        Result cancelResult = doc.Cancel();
        cancelResult.IsSuccess.ShouldBeTrue();
        doc.DocumentStatus.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public void WarehouseDocument_IllegalTransitions_Should_Fail()
    {
        // 1. Create Draft
        var doc = WarehouseDocument.CreateDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DocumentType.Receiving,
            "SYS-1");

        // Draft cannot directly be marked as Posted without posting gate
        doc.ValidateForPosting().IsFailure.ShouldBeTrue();

        // Cancelled document cannot be Submitted
        doc.Cancel();
        doc.Submit().IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void InventoryCount_LegalAndIllegalTransitions_Should_BeEnforced()
    {
        DateTime now = DateTime.UtcNow;

        Result<InventoryCount> countResult = InventoryCount.Plan(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            InventoryCountType.Scheduled,
            InventoryCountScopeType.EntireWarehouse,
            null,
            FreezePolicy.SoftFreeze,
            now);

        InventoryCount count = countResult.Value;
        count.Status.ShouldBe(InventoryCountStatus.Planned);

        // Planned -> InProgress
        count.Start(now.AddMinutes(5)).IsSuccess.ShouldBeTrue();
        count.Status.ShouldBe(InventoryCountStatus.InProgress);

        // InProgress -> Completed
        count.Complete(now.AddMinutes(30)).IsSuccess.ShouldBeTrue();
        count.Status.ShouldBe(InventoryCountStatus.Completed);

        // Completed -> Closed
        count.Close(now.AddHours(1)).IsSuccess.ShouldBeTrue();
        count.Status.ShouldBe(InventoryCountStatus.Closed);

        // Closed count cannot be Started again
        count.Start(now.AddHours(2)).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Custody_Transitions_Should_BeEnforced()
    {
        DateTime now = DateTime.UtcNow;

        Result<Custody> custodyResult = Custody.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PartyType.Employee,
            Guid.NewGuid(),
            CustodyKind.Personal,
            Guid.NewGuid(),
            now);

        Custody custody = custodyResult.Value;
        custody.Status.ShouldBe(CustodyStatus.Active);

        // Active -> Closed
        custody.Close(Guid.NewGuid(), now.AddDays(1)).IsSuccess.ShouldBeTrue();
        custody.Status.ShouldBe(CustodyStatus.Closed);

        // Closed -> Reopen
        custody.Reopen().IsSuccess.ShouldBeTrue();
        custody.Status.ShouldBe(CustodyStatus.Active);
    }
}

using Application.Abstractions.Authentication;
using Application.Abstractions.Assets;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Recipients;
using Application.Custodies.Assign;
using Application.DocumentLineAssetSelections.Add;
using Application.DocumentLineAssetSelections.Remove;
using Application.ReturnInfos.Upsert;
using Application.UnitTests.Abstractions;
using Domain.AssetMovementHistories;
using Domain.Assets;
using Domain.Common;
using Domain.Custodies;
using Domain.CustodyHistories;
using Domain.DocumentLineAssetSelections;
using Domain.DocumentLines;
using Domain.ReturnInfos;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.M6;

public sealed class M6DomainAndHandlerTests : BaseHandlerTest
{
    [Fact]
    public void Custody_Should_OpenCloseAndReopen_WithEvents_WhenTransitionIsValid()
    {
        // Arrange
        var custodyId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        DateTime fromUtc = DateTime.UtcNow.AddMinutes(-2);

        // Act
        Result<Custody> openResult = Custody.Open(
            custodyId,
            assetId,
            PartyType.OrganizationalUnit,
            Guid.NewGuid(),
            CustodyKind.Operational,
            Guid.NewGuid(),
            fromUtc);
        Result closeResult = openResult.Value.Close(Guid.NewGuid(), fromUtc.AddMinutes(1));
        Result reopenResult = openResult.Value.Reopen();

        // Assert
        openResult.IsSuccess.ShouldBeTrue();
        closeResult.IsSuccess.ShouldBeTrue();
        reopenResult.IsSuccess.ShouldBeTrue();
        openResult.Value.Status.ShouldBe(CustodyStatus.Active);
        openResult.Value.RowVersion.ShouldBe(3);
        openResult.Value.DomainEvents.ShouldContain(domainEvent => domainEvent is CustodyOpenedDomainEvent);
        openResult.Value.DomainEvents.ShouldContain(domainEvent => domainEvent is CustodyClosedDomainEvent);
        openResult.Value.DomainEvents.ShouldContain(domainEvent => domainEvent is CustodyReopenedDomainEvent);
    }

    [Fact]
    public void Custody_Should_RejectPersonalCustodyForNonEmployee()
    {
        // Arrange

        // Act
        Result<Custody> result = Custody.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PartyType.Site,
            Guid.NewGuid(),
            CustodyKind.Personal,
            Guid.NewGuid(),
            DateTime.UtcNow);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CustodyErrors.PersonalRequiresEmployee);
    }

    [Fact]
    public void Custody_Should_RejectCloseAtOrBeforeOpeningTime()
    {
        // Arrange
        DateTime fromUtc = DateTime.UtcNow;
        Custody custody = Custody.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PartyType.Employee,
            Guid.NewGuid(),
            CustodyKind.Personal,
            Guid.NewGuid(),
            fromUtc).Value;

        // Act
        Result result = custody.Close(Guid.NewGuid(), fromUtc);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CustodyErrors.CloseTimeInvalid);
    }

    [Fact]
    public void AssetMovementHistory_Should_CreateAndRaiseEvent_WhenValuesAreValid()
    {
        // Arrange
        var historyId = Guid.NewGuid();

        // Act
        Result<AssetMovementHistory> result = AssetMovementHistory.Create(
            historyId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            AssetMovementType.Issued,
            DateTime.UtcNow);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.MovementType.ShouldBe(AssetMovementType.Issued);
        result.Value.DomainEvents.ShouldContain(domainEvent => domainEvent is AssetMovementHistoryAppendedDomainEvent);
    }

    [Fact]
    public void AssetMovementHistory_Should_RejectUnknownMovementType()
    {
        // Arrange

        // Act
        Result<AssetMovementHistory> result = AssetMovementHistory.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            (AssetMovementType)999,
            DateTime.UtcNow);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AssetMovementHistoryErrors.MovementTypeInvalid);
    }

    [Fact]
    public void CustodyHistory_Should_NormalizeNoteAndRaiseEvent_WhenTransitionIsValid()
    {
        // Arrange

        // Act
        Result<CustodyHistory> result = CustodyHistory.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CustodyStatus.Active,
            CustodyStatus.Closed,
            Guid.NewGuid(),
            DateTime.UtcNow,
            "  Assigned to employee  ");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Note.ShouldBe("Assigned to employee");
        result.Value.DomainEvents.ShouldContain(domainEvent => domainEvent is CustodyHistoryRecordedDomainEvent);
    }

    [Fact]
    public void CustodyHistory_Should_RejectSameStatusTransition()
    {
        // Arrange

        // Act
        Result<CustodyHistory> result = CustodyHistory.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CustodyStatus.Active,
            CustodyStatus.Active,
            Guid.NewGuid(),
            DateTime.UtcNow,
            null);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CustodyHistoryErrors.TransitionInvalid);
    }

    [Fact]
    public void ReturnInfo_Should_CreateUpdateAndRaiseEvents_WhenValuesAreValid()
    {
        // Arrange
        var documentId = Guid.NewGuid();

        // Act
        Result<ReturnInfo> createResult = ReturnInfo.Create(documentId, Guid.NewGuid(), "  Returned unused  ");
        Result updateResult = createResult.Value.Update(Guid.NewGuid(), "  Reassigned return reason  ");

        // Assert
        createResult.IsSuccess.ShouldBeTrue();
        updateResult.IsSuccess.ShouldBeTrue();
        createResult.Value.ReturnReason.ShouldBe("Reassigned return reason");
        createResult.Value.DomainEvents.ShouldContain(domainEvent => domainEvent is ReturnInfoCreatedDomainEvent);
        createResult.Value.DomainEvents.ShouldContain(domainEvent => domainEvent is ReturnInfoUpdatedDomainEvent);
    }

    [Fact]
    public void ReturnInfo_Should_RejectBlankReason()
    {
        // Arrange

        // Act
        Result<ReturnInfo> result = ReturnInfo.Create(Guid.NewGuid(), Guid.NewGuid(), "   ");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ReturnInfoErrors.ReturnReasonInvalid);
    }

    [Fact]
    public void DocumentLineAssetSelection_Should_CreateAndRaiseSelectionAndRemovalEvents()
    {
        // Arrange

        // Act
        Result<DocumentLineAssetSelection> result = DocumentLineAssetSelection.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());
        result.Value.RaiseRemovedEvent();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.DomainEvents.ShouldContain(domainEvent => domainEvent is DocumentLineAssetSelectedDomainEvent);
        result.Value.DomainEvents.ShouldContain(domainEvent => domainEvent is DocumentLineAssetSelectionRemovedDomainEvent);
    }

    [Fact]
    public void DocumentLineAssetSelection_Should_RejectEmptyIdentity()
    {
        // Arrange

        // Act
        Result<DocumentLineAssetSelection> result = DocumentLineAssetSelection.Create(
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DocumentLineAssetSelectionErrors.IdentityRequired);
    }

    [Fact]
    public void AssignAssetCustodyValidator_Should_RejectInvalidCommandAndAcceptValidCommand()
    {
        // Arrange
        var validator = new AssignAssetCustodyCommandValidator();
        var invalid = new AssignAssetCustodyCommand(Guid.Empty, Guid.Empty, 0, new string('x', 301));
        var valid = new AssignAssetCustodyCommand(Guid.NewGuid(), Guid.NewGuid(), 1, "Assigned");

        // Act
        FluentValidation.Results.ValidationResult invalidResult = validator.Validate(invalid);
        FluentValidation.Results.ValidationResult validResult = validator.Validate(valid);

        // Assert
        invalidResult.IsValid.ShouldBeFalse();
        invalidResult.Errors.Select(error => error.PropertyName).ShouldBe([
            nameof(invalid.AssetId),
            nameof(invalid.EmployeeId),
            nameof(invalid.ExpectedCustodyRowVersion),
            nameof(invalid.Note)]);
        validResult.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void AddDocumentLineAssetSelectionValidator_Should_RejectInvalidCommandAndAcceptValidCommand()
    {
        // Arrange
        var validator = new AddDocumentLineAssetSelectionCommandValidator();
        var invalid = new AddDocumentLineAssetSelectionCommand(Guid.Empty, Guid.Empty, Guid.Empty, 0);
        var valid = new AddDocumentLineAssetSelectionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);

        // Act
        FluentValidation.Results.ValidationResult invalidResult = validator.Validate(invalid);
        FluentValidation.Results.ValidationResult validResult = validator.Validate(valid);

        // Assert
        invalidResult.IsValid.ShouldBeFalse();
        invalidResult.Errors.Select(error => error.PropertyName).ShouldBe([
            nameof(invalid.DocumentId),
            nameof(invalid.LineId),
            nameof(invalid.AssetId),
            nameof(invalid.ExpectedRowVersion)]);
        validResult.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void RemoveDocumentLineAssetSelectionValidator_Should_RejectInvalidCommandAndAcceptValidCommand()
    {
        // Arrange
        var validator = new RemoveDocumentLineAssetSelectionCommandValidator();
        var invalid = new RemoveDocumentLineAssetSelectionCommand(Guid.Empty, Guid.Empty, Guid.Empty, 0);
        var valid = new RemoveDocumentLineAssetSelectionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);

        // Act
        FluentValidation.Results.ValidationResult invalidResult = validator.Validate(invalid);
        FluentValidation.Results.ValidationResult validResult = validator.Validate(valid);

        // Assert
        invalidResult.IsValid.ShouldBeFalse();
        invalidResult.Errors.Select(error => error.PropertyName).ShouldBe([
            nameof(invalid.DocumentId),
            nameof(invalid.LineId),
            nameof(invalid.AssetId),
            nameof(invalid.ExpectedRowVersion)]);
        validResult.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void UpsertReturnInfoValidator_Should_RejectInvalidCommandAndAcceptValidCommand()
    {
        // Arrange
        var validator = new UpsertReturnInfoCommandValidator();
        var invalid = new UpsertReturnInfoCommand(Guid.Empty, Guid.Empty, string.Empty, 0);
        var valid = new UpsertReturnInfoCommand(Guid.NewGuid(), Guid.NewGuid(), "Return", 1);

        // Act
        FluentValidation.Results.ValidationResult invalidResult = validator.Validate(invalid);
        FluentValidation.Results.ValidationResult validResult = validator.Validate(valid);

        // Assert
        invalidResult.IsValid.ShouldBeFalse();
        invalidResult.Errors.Select(error => error.PropertyName).ShouldBe([
            nameof(invalid.DocumentId),
            nameof(invalid.OriginalIssueDocumentId),
            nameof(invalid.ReturnReason),
            nameof(invalid.ExpectedRowVersion)]);
        validResult.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task UpsertReturnInfo_Should_PersistNormalizedInfoAndAdvanceVersion_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var warehouseId = Guid.NewGuid();
        WarehouseDocument originalIssue = CreatePostedIssue(warehouseId);
        WarehouseDocument returnDocument = CreateDraftDocument(warehouseId, DocumentType.Return);
        context.AddRange(originalIssue, returnDocument);
        await context.SaveChangesAsync();
        var command = new UpsertReturnInfoCommand(
            returnDocument.Id,
            originalIssue.Id,
            "  Not used  ",
            returnDocument.RowVersion);

        // Act
        Result result = await CreateReturnInfoHandler(context, authorized: true).Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        ReturnInfo info = await context.ReturnInfos.SingleAsync();
        info.OriginalIssueDocumentId.ShouldBe(originalIssue.Id);
        info.ReturnReason.ShouldBe("Not used");
        info.DomainEvents.ShouldContain(domainEvent => domainEvent is ReturnInfoCreatedDomainEvent);
        (await context.WarehouseDocuments.SingleAsync(item => item.Id == returnDocument.Id)).RowVersion.ShouldBe(2);
    }

    [Fact]
    public async Task UpsertReturnInfo_Should_HideDocumentAndNotPersist_WhenCallerLacksEditScope()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var warehouseId = Guid.NewGuid();
        WarehouseDocument originalIssue = CreatePostedIssue(warehouseId);
        WarehouseDocument returnDocument = CreateDraftDocument(warehouseId, DocumentType.Return);
        context.AddRange(originalIssue, returnDocument);
        await context.SaveChangesAsync();
        var command = new UpsertReturnInfoCommand(returnDocument.Id, originalIssue.Id, "Reason", 1);

        // Act
        Result result = await CreateReturnInfoHandler(context, authorized: false).Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(WarehouseDocumentErrors.NotFound(returnDocument.Id));
        (await context.ReturnInfos.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task AddDocumentLineAssetSelection_Should_PersistReturnSelectionAndAdvanceDocumentVersion_WhenEligible()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var warehouseId = Guid.NewGuid();
        WarehouseDocument originalIssue = CreatePostedIssue(warehouseId);
        WarehouseDocument returnDocument = CreateDraftDocument(warehouseId, DocumentType.Return);
        DocumentLine line = CreateAssetLine(returnDocument.Id, Guid.NewGuid());
        Asset asset = CreateAsset(line.MaterialId, warehouseId);
        Custody custody = Custody.Open(
            Guid.NewGuid(),
            asset.Id,
            PartyType.OrganizationalUnit,
            Guid.NewGuid(),
            CustodyKind.Operational,
            originalIssue.Id,
            DateTime.UtcNow.AddMinutes(-1)).Value;
        ReturnInfo returnInfo = ReturnInfo.Create(returnDocument.Id, originalIssue.Id, "Reason").Value;
        context.AddRange(originalIssue, returnDocument, line, asset, custody, returnInfo);
        await context.SaveChangesAsync();
        var command = new AddDocumentLineAssetSelectionCommand(
            returnDocument.Id,
            line.Id,
            asset.Id,
            returnDocument.RowVersion);

        // Act
        Result<Guid> result = await CreateSelectionAddHandler(context, authorized: true).Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        DocumentLineAssetSelection selection = await context.DocumentLineAssetSelections.SingleAsync();
        selection.AssetId.ShouldBe(asset.Id);
        selection.DomainEvents.ShouldContain(domainEvent => domainEvent is DocumentLineAssetSelectedDomainEvent);
        (await context.WarehouseDocuments.SingleAsync(item => item.Id == returnDocument.Id)).RowVersion.ShouldBe(2);
    }

    [Fact]
    public async Task RemoveDocumentLineAssetSelection_Should_RemoveSelectionAndAdvanceDocumentVersion_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        WarehouseDocument document = CreateDraftDocument(Guid.NewGuid(), DocumentType.Issue);
        DocumentLine line = CreateAssetLine(document.Id, Guid.NewGuid());
        DocumentLineAssetSelection selection = DocumentLineAssetSelection.Create(
            Guid.NewGuid(), document.Id, line.Id, Guid.NewGuid()).Value;
        context.AddRange(document, line, selection);
        await context.SaveChangesAsync();
        var command = new RemoveDocumentLineAssetSelectionCommand(
            document.Id,
            line.Id,
            selection.AssetId,
            document.RowVersion);

        // Act
        Result result = await CreateSelectionRemoveHandler(context, authorized: true).Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.DocumentLineAssetSelections.CountAsync()).ShouldBe(0);
        selection.DomainEvents.ShouldContain(domainEvent => domainEvent is DocumentLineAssetSelectionRemovedDomainEvent);
        (await context.WarehouseDocuments.SingleAsync()).RowVersion.ShouldBe(2);
    }

    [Fact]
    public async Task AssignAssetCustody_Should_CloseOperationalCustodyOpenPersonalAndRecordHistory_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var warehouseId = Guid.NewGuid();
        WarehouseDocument issue = CreatePostedIssue(warehouseId);
        var assetId = Guid.NewGuid();
        Custody operational = Custody.Open(
            Guid.NewGuid(),
            assetId,
            PartyType.OrganizationalUnit,
            Guid.NewGuid(),
            CustodyKind.Operational,
            issue.Id,
            DateTime.UtcNow.AddMinutes(-5)).Value;
        context.AddRange(issue, operational);
        await context.SaveChangesAsync();
        DateTime nowUtc = DateTime.UtcNow;
        var command = new AssignAssetCustodyCommand(assetId, Guid.NewGuid(), operational.RowVersion, "  Assigned  ");

        // Act
        Result<Guid> result = await CreateAssignHandler(context, nowUtc).Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        List<Custody> custodies = await context.Custodies.OrderBy(item => item.FromUtc).ToListAsync();
        custodies.Count.ShouldBe(2);
        custodies.Single(item => item.Id == operational.Id).Status.ShouldBe(CustodyStatus.Closed);
        Custody personal = custodies.Single(item => item.Id == result.Value);
        personal.CustodyKind.ShouldBe(CustodyKind.Personal);
        personal.HolderType.ShouldBe(PartyType.Employee);
        personal.Status.ShouldBe(CustodyStatus.Active);
        CustodyHistory history = await context.CustodyHistories.SingleAsync();
        history.Note.ShouldBe("Assigned");
        history.FromStatus.ShouldBe(CustodyStatus.Active);
        history.ToStatus.ShouldBe(CustodyStatus.Closed);
    }

    [Fact]
    public async Task AssignAssetCustody_Should_ReturnRowVersionMismatch_WhenVersionIsStale()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var warehouseId = Guid.NewGuid();
        WarehouseDocument issue = CreatePostedIssue(warehouseId);
        var assetId = Guid.NewGuid();
        Custody custody = Custody.Open(
            Guid.NewGuid(), assetId, PartyType.Site, Guid.NewGuid(), CustodyKind.Operational,
            issue.Id, DateTime.UtcNow.AddMinutes(-5)).Value;
        context.AddRange(issue, custody);
        await context.SaveChangesAsync();
        var command = new AssignAssetCustodyCommand(assetId, Guid.NewGuid(), custody.RowVersion + 1, null);

        // Act
        Result<Guid> result = await CreateAssignHandler(context, DateTime.UtcNow).Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CustodyErrors.RowVersionMismatch(custody.Id, custody.RowVersion + 1, custody.RowVersion));
        (await context.Custodies.CountAsync()).ShouldBe(1);
        (await context.CustodyHistories.CountAsync()).ShouldBe(0);
    }

    private static WarehouseDocument CreateDraftDocument(Guid warehouseId, DocumentType documentType) =>
        WarehouseDocument.CreateDraft(Guid.NewGuid(), warehouseId, documentType, $"DOC-{Guid.NewGuid():N}");

    private static WarehouseDocument CreatePostedIssue(Guid warehouseId)
    {
        WarehouseDocument document = CreateDraftDocument(warehouseId, DocumentType.Issue);
        document.SetSignedCopy(Guid.NewGuid()).IsSuccess.ShouldBeTrue();
        document.UpdatePaperReference("ISS-1", 2026).IsSuccess.ShouldBeTrue();
        document.Submit().IsSuccess.ShouldBeTrue();
        document.MarkPosted(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-10)).IsSuccess.ShouldBeTrue();
        return document;
    }

    private static DocumentLine CreateAssetLine(Guid documentId, Guid materialId) =>
        DocumentLine.Create(
            Guid.NewGuid(), documentId, materialId, DocumentLineType.Asset, 1m, null, 1m, null, null, null).Value;

    private static Asset CreateAsset(Guid materialId, Guid warehouseId) =>
        Asset.CreateReceived(
            Guid.NewGuid(), materialId, warehouseId, Guid.NewGuid(), $"AST-{Guid.NewGuid():N}", new DateOnly(2026, 8, 20)).Value;

    private static UpsertReturnInfoCommandHandler CreateReturnInfoHandler(TestDbContext context, bool authorized)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        return new UpsertReturnInfoCommandHandler(
            context,
            userContext,
            CreateScopeAuthorization(authorized),
            Substitute.For<IDatabaseExceptionClassifier>());
    }

    private static AddDocumentLineAssetSelectionCommandHandler CreateSelectionAddHandler(
        TestDbContext context,
        bool authorized)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        return new AddDocumentLineAssetSelectionCommandHandler(
            context,
            userContext,
            CreateScopeAuthorization(authorized),
            Substitute.For<IDatabaseExceptionClassifier>());
    }

    private static RemoveDocumentLineAssetSelectionCommandHandler CreateSelectionRemoveHandler(
        TestDbContext context,
        bool authorized)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        return new RemoveDocumentLineAssetSelectionCommandHandler(context, userContext, CreateScopeAuthorization(authorized));
    }

    private static AssignAssetCustodyCommandHandler CreateAssignHandler(TestDbContext context, DateTime nowUtc)
    {
        IApplicationTransaction transaction = Substitute.For<IApplicationTransaction>();
        transaction.ExecuteAsync(
                Arg.Any<Func<CancellationToken, Task<Result<Guid>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.ArgAt<Func<CancellationToken, Task<Result<Guid>>>>(0)(
                callInfo.ArgAt<CancellationToken>(1)));
        IAssetKeyLock assetKeyLock = Substitute.For<IAssetKeyLock>();
        assetKeyLock.AcquireAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        IActivePartyLookup partyLookup = Substitute.For<IActivePartyLookup>();
        partyLookup.GetStatusAsync(PartyType.Employee, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ActivePartyLookupStatus.Active);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(nowUtc);

        return new AssignAssetCustodyCommandHandler(
            context,
            transaction,
            assetKeyLock,
            userContext,
            CreateScopeAuthorization(true),
            partyLookup,
            dateTimeProvider,
            Substitute.For<IDatabaseExceptionClassifier>());
    }

    private static IScopeAuthorizationService CreateScopeAuthorization(bool authorized)
    {
        IScopeAuthorizationService authorization = Substitute.For<IScopeAuthorizationService>();
        authorization.HasPermissionInScopeAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<ScopeType>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Returns(authorized);
        return authorization;
    }
}

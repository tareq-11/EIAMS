using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Recipients;
using Application.IssueTos;
using Application.IssueTos.Upsert;
using Application.UnitTests.Abstractions;
using Domain.Common;
using Domain.IssueTos;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.M5;

public sealed class IssueToSliceTests : BaseHandlerTest
{
    [Fact]
    public void IssueTo_Should_RejectUnknownRecipientType_WhenCreated()
    {
        // Arrange
        var documentId = Guid.NewGuid();

        // Act
        Result<IssueTo> result = IssueTo.Create(documentId, (PartyType)999, Guid.NewGuid(), "Maintenance");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(IssueToErrors.RecipientTypeInvalid);
    }

    [Fact]
    public void IssueTo_Should_RejectTooLongReason_WhenUpdated()
    {
        // Arrange
        Result<IssueTo> createResult = IssueTo.Create(
            Guid.NewGuid(),
            PartyType.Employee,
            Guid.NewGuid(),
            "Maintenance");

        // Act
        Result result = createResult.Value.Update(PartyType.Employee, Guid.NewGuid(), new string('x', 201));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(IssueToErrors.IssueReasonInvalid);
    }

    [Fact]
    public async Task UpsertIssueTo_Should_ReturnNotFoundAndNotPersist_WhenCallerLacksSourceEditScope()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        WarehouseDocument document = AddDocument(context, DocumentType.Issue);
        await context.SaveChangesAsync();
        UpsertIssueToCommandHandler handler = CreateHandler(context, false, ActivePartyLookupStatus.Active);
        var command = new UpsertIssueToCommand(
            document.Id,
            PartyType.Employee,
            Guid.NewGuid(),
            "Maintenance",
            document.RowVersion);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(WarehouseDocumentErrors.NotFound(document.Id));
        (await context.IssueTos.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task UpsertIssueTo_Should_ReturnInactiveRecipientAndNotPersist_WhenLookupReturnsInactive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        WarehouseDocument document = AddDocument(context, DocumentType.Issue);
        await context.SaveChangesAsync();
        var recipientId = Guid.NewGuid();
        UpsertIssueToCommandHandler handler = CreateHandler(context, true, ActivePartyLookupStatus.Inactive);
        var command = new UpsertIssueToCommand(
            document.Id,
            PartyType.Employee,
            recipientId,
            "Maintenance",
            document.RowVersion);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(IssueToErrors.RecipientInactive(PartyType.Employee, recipientId));
        (await context.IssueTos.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task UpsertIssueTo_Should_ReturnWrongDocumentTypeAndNotPersist_WhenDocumentIsNotIssue()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        WarehouseDocument document = AddDocument(context, DocumentType.Receiving);
        await context.SaveChangesAsync();
        UpsertIssueToCommandHandler handler = CreateHandler(context, true, ActivePartyLookupStatus.Active);
        var command = new UpsertIssueToCommand(
            document.Id,
            PartyType.Employee,
            Guid.NewGuid(),
            "Maintenance",
            document.RowVersion);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(IssueToErrors.WrongDocumentType(document.Id));
        (await context.IssueTos.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task UpsertIssueTo_Should_ReturnRecipientRequiredWithoutCallingLookup_WhenRecipientIdIsEmpty()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        WarehouseDocument document = AddDocument(context, DocumentType.Issue);
        await context.SaveChangesAsync();
        UpsertIssueToCommandHandler handler = CreateHandler(context, true, ActivePartyLookupStatus.Active);
        var command = new UpsertIssueToCommand(
            document.Id,
            PartyType.Employee,
            Guid.Empty,
            "Maintenance",
            document.RowVersion);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(IssueToErrors.RecipientRequired);
        (await context.IssueTos.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task UpsertIssueTo_Should_ReturnRecipientTypeInvalidWithoutCallingLookup_WhenRecipientTypeIsUnknown()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        WarehouseDocument document = AddDocument(context, DocumentType.Issue);
        await context.SaveChangesAsync();
        UpsertIssueToCommandHandler handler = CreateHandler(context, true, ActivePartyLookupStatus.Active);
        var command = new UpsertIssueToCommand(
            document.Id,
            (PartyType)999,
            Guid.NewGuid(),
            "Maintenance",
            document.RowVersion);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(IssueToErrors.RecipientTypeInvalid);
        (await context.IssueTos.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task UpsertIssueTo_Should_PersistNormalizedDetailAndAdvanceDocumentVersion_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        WarehouseDocument document = AddDocument(context, DocumentType.Issue);
        await context.SaveChangesAsync();
        var recipientId = Guid.NewGuid();
        UpsertIssueToCommandHandler handler = CreateHandler(context, true, ActivePartyLookupStatus.Active);
        int expectedRowVersion = document.RowVersion;
        var command = new UpsertIssueToCommand(
            document.Id,
            PartyType.Employee,
            recipientId,
            "  Maintenance  ",
            expectedRowVersion);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        IssueTo persisted = await context.IssueTos.SingleAsync();
        persisted.RecipientId.ShouldBe(recipientId);
        persisted.IssueReason.ShouldBe("Maintenance");
        persisted.DomainEvents.ShouldContain(domainEvent => domainEvent is IssueToCreatedDomainEvent);
        (await context.WarehouseDocuments.SingleAsync()).RowVersion.ShouldBe(expectedRowVersion + 1);
    }

    [Fact]
    public async Task UpsertIssueTo_Should_NotAdvanceDocumentVersion_WhenRequestIsUnchanged()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        WarehouseDocument document = AddDocument(context, DocumentType.Issue);
        var recipientId = Guid.NewGuid();
        Result<IssueTo> issueToResult = IssueTo.Create(document.Id, PartyType.Employee, recipientId, "Maintenance");
        context.IssueTos.Add(issueToResult.Value);
        await context.SaveChangesAsync();
        UpsertIssueToCommandHandler handler = CreateHandler(context, true, ActivePartyLookupStatus.Active);
        var command = new UpsertIssueToCommand(
            document.Id,
            PartyType.Employee,
            recipientId,
            "Maintenance",
            document.RowVersion);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.WarehouseDocuments.SingleAsync()).RowVersion.ShouldBe(document.RowVersion);
    }

    [Theory]
    [InlineData(PartyType.Employee, true)]
    [InlineData((PartyType)999, false)]
    public async Task UpsertIssueToValidator_Should_ValidateRecipientType(PartyType recipientType, bool expectedValid)
    {
        // Arrange
        var command = new UpsertIssueToCommand(
            Guid.NewGuid(),
            recipientType,
            Guid.NewGuid(),
            "Maintenance",
            1);
        var validator = new UpsertIssueToCommandValidator();

        // Act
        FluentValidation.Results.ValidationResult result = await validator.ValidateAsync(command);

        // Assert
        result.IsValid.ShouldBe(expectedValid);
    }

    [Fact]
    public async Task IssueSubmissionValidator_Should_ReturnRequired_WhenIssueToDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        WarehouseDocument document = AddDocument(context, DocumentType.Issue);
        await context.SaveChangesAsync();
        IActivePartyLookup lookup = Substitute.For<IActivePartyLookup>();
        var validator = new IssueSubmissionValidator(context, lookup);

        // Act
        Result result = await validator.ValidateAsync(document, [], CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(IssueToErrors.Required(document.Id));
    }

    private static WarehouseDocument AddDocument(TestDbContext context, DocumentType documentType)
    {
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            documentType,
            $"DOC-{Guid.NewGuid():N}");
        context.WarehouseDocuments.Add(document);
        return document;
    }

    private static UpsertIssueToCommandHandler CreateHandler(
        TestDbContext context,
        bool authorized,
        ActivePartyLookupStatus recipientStatus)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        IScopeAuthorizationService authorization = Substitute.For<IScopeAuthorizationService>();
        authorization.HasPermissionInScopeAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<ScopeType>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(authorized));
        IActivePartyLookup lookup = Substitute.For<IActivePartyLookup>();
        lookup.GetStatusAsync(
                Arg.Any<PartyType>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(recipientStatus));
        IDatabaseExceptionClassifier classifier = Substitute.For<IDatabaseExceptionClassifier>();

        return new UpsertIssueToCommandHandler(context, userContext, authorization, lookup, classifier);
    }
}

using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.TransferInfos.Upsert;
using Application.UnitTests.Abstractions;
using Domain.Common;
using Domain.TransferInfos;
using Domain.Warehouses;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.M5;

public sealed class TransferInfoHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_CreateTransferInfoAndAdvanceDocumentVersion()
    {
        await using TestDbContext context = CreateDbContext();
        (WarehouseDocument document, Warehouse destination) = await SeedTransferAsync(context);
        var command = new UpsertTransferInfoCommand(document.Id, destination.Id, "  Replenishment  ", 1);

        Result result = await CreateHandler(context, authorized: true).Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        TransferInfo info = await context.TransferInfos.SingleAsync();
        info.DestinationWarehouseId.ShouldBe(destination.Id);
        info.TransferReason.ShouldBe("Replenishment");
        (await context.WarehouseDocuments.SingleAsync()).RowVersion.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_Should_ReturnHiddenNotFound_WhenSourceScopeIsUnauthorized()
    {
        await using TestDbContext context = CreateDbContext();
        (WarehouseDocument document, Warehouse destination) = await SeedTransferAsync(context);
        var command = new UpsertTransferInfoCommand(document.Id, destination.Id, "Reason", 1);

        Result result = await CreateHandler(context, authorized: false).Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(WarehouseDocumentErrors.NotFound(document.Id));
        (await context.TransferInfos.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_RejectDestinationEqualToSource()
    {
        await using TestDbContext context = CreateDbContext();
        (WarehouseDocument document, _) = await SeedTransferAsync(context);
        var command = new UpsertTransferInfoCommand(document.Id, document.WarehouseId, "Reason", 1);

        Result result = await CreateHandler(context, authorized: true).Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TransferInfoErrors.DestinationSameAsSource(document.Id, document.WarehouseId));
    }

    [Fact]
    public async Task Handle_Should_RejectInactiveDestination()
    {
        await using TestDbContext context = CreateDbContext();
        (WarehouseDocument document, Warehouse destination) = await SeedTransferAsync(context);
        destination.SetStatus(Status.Inactive);
        await context.SaveChangesAsync();
        var command = new UpsertTransferInfoCommand(document.Id, destination.Id, "Reason", 1);

        Result result = await CreateHandler(context, authorized: true).Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(WarehouseErrors.Inactive(destination.Id));
    }

    [Fact]
    public async Task Handle_Should_NotAdvanceVersion_WhenPayloadHasNoChanges()
    {
        await using TestDbContext context = CreateDbContext();
        (WarehouseDocument document, Warehouse destination) = await SeedTransferAsync(context);
        context.TransferInfos.Add(TransferInfo.Create(document.Id, destination.Id, "Reason").Value);
        await context.SaveChangesAsync();
        var command = new UpsertTransferInfoCommand(document.Id, destination.Id, "  Reason  ", 1);

        Result result = await CreateHandler(context, authorized: true).Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        (await context.WarehouseDocuments.SingleAsync()).RowVersion.ShouldBe(1);
    }

    [Fact]
    public void Validator_Should_AcceptACompleteRequest()
    {
        var command = new UpsertTransferInfoCommand(Guid.NewGuid(), Guid.NewGuid(), "Reason", 1);

        FluentValidation.Results.ValidationResult result = new UpsertTransferInfoCommandValidator().Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validator_Should_RejectEmptyIdsReasonAndNonPositiveVersion()
    {
        var command = new UpsertTransferInfoCommand(Guid.Empty, Guid.Empty, string.Empty, 0);

        FluentValidation.Results.ValidationResult result = new UpsertTransferInfoCommandValidator().Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName).ShouldBe([
            nameof(command.DocumentId),
            nameof(command.DestinationWarehouseId),
            nameof(command.TransferReason),
            nameof(command.ExpectedRowVersion)]);
    }

    private static UpsertTransferInfoCommandHandler CreateHandler(TestDbContext context, bool authorized)
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
            .Returns(authorized);

        return new UpsertTransferInfoCommandHandler(
            context,
            userContext,
            authorization,
            Substitute.For<IDatabaseExceptionClassifier>());
    }

    private static async Task<(WarehouseDocument Document, Warehouse Destination)> SeedTransferAsync(
        TestDbContext context)
    {
        var siteId = Guid.NewGuid();
        var source = Warehouse.Create(Guid.NewGuid(), siteId, "Source", "SRC", "Main", true);
        var destination = Warehouse.Create(Guid.NewGuid(), siteId, "Destination", "DST", "Main", true);
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(),
            source.Id,
            DocumentType.Transfer,
            "TRF-2026-000001");

        context.AddRange(source, destination, document);
        await context.SaveChangesAsync();

        return (document, destination);
    }
}

using Application.Abstractions.Assets;
using Application.DocumentLines;
using Application.DocumentLines.Add;
using Application.DocumentLines.Update;
using Domain.Common;
using Domain.ReceivingInfos;
using SharedKernel;

namespace Application.UnitTests.M4;

public sealed class ReceivingAndOpeningRulesTests
{
    [Fact]
    public void ReceivingInfo_Should_TrimValuesAndRaiseCreatedEvent()
    {
        Result<ReceivingInfo> result = ReceivingInfo.Create(
            Guid.NewGuid(),
            "  Supplier A  ",
            "  INV-1  ",
            ReceivingType.Supplier);

        result.IsSuccess.ShouldBeTrue();
        result.Value.SupplierRef.ShouldBe("Supplier A");
        result.Value.SupplierInvoiceRef.ShouldBe("INV-1");
        result.Value.DomainEvents.ShouldContain(domainEvent => domainEvent is ReceivingInfoCreatedDomainEvent);
    }

    [Fact]
    public void ReceivingInfo_Should_RejectBlankSupplierReference()
    {
        Result<ReceivingInfo> result = ReceivingInfo.Create(
            Guid.NewGuid(),
            "   ",
            null,
            ReceivingType.Supplier);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ReceivingInfoErrors.SupplierRefInvalid);
    }

    [Fact]
    public void ReceivingInfo_Should_RejectUnknownReceivingType()
    {
        Result<ReceivingInfo> result = ReceivingInfo.Create(
            Guid.NewGuid(),
            "Supplier A",
            null,
            (ReceivingType)999);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ReceivingInfoErrors.ReceivingTypeInvalid);
    }

    [Fact]
    public void OpeningLineRules_Should_RequireOpeningTypeForOpeningDocument()
    {
        Result result = OpeningLineRules.Validate(DocumentType.Opening, Guid.NewGuid(), null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("DocumentLines.OpeningTypeRequired");
    }

    [Fact]
    public void OpeningLineRules_Should_RejectOpeningTypeForReceivingDocument()
    {
        Result result = OpeningLineRules.Validate(
            DocumentType.Receiving,
            Guid.NewGuid(),
            OpeningType.Initial);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("DocumentLines.OpeningTypeNotAllowed");
    }

    [Fact]
    public void OpeningLineRules_Should_RejectUnknownOpeningType()
    {
        Result result = OpeningLineRules.Validate(
            DocumentType.Opening,
            Guid.NewGuid(),
            (OpeningType)999);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("DocumentLines.OpeningTypeInvalid");
    }

    [Fact]
    public async Task AddLineValidator_Should_RejectUnknownOpeningType()
    {
        var command = new AddDocumentLineCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1m,
            null,
            null,
            null,
            null,
            (OpeningType)999,
            1);

        FluentValidation.Results.ValidationResult result =
            await new AddDocumentLineCommandValidator().ValidateAsync(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.PropertyName == nameof(command.OpeningType));
    }

    [Fact]
    public async Task UpdateLineValidator_Should_RejectUnknownOpeningType()
    {
        var command = new UpdateDocumentLineCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1m,
            null,
            null,
            null,
            null,
            (OpeningType)999,
            1);

        FluentValidation.Results.ValidationResult result =
            await new UpdateDocumentLineCommandValidator().ValidateAsync(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.PropertyName == nameof(command.OpeningType));
    }

    [Theory]
    [InlineData(MovementType.Issue, true)]
    [InlineData(MovementType.TransferOut, true)]
    [InlineData(MovementType.AdjustmentOut, true)]
    [InlineData(MovementType.Receipt, false)]
    [InlineData(MovementType.Opening, false)]
    public void AssetDownstreamUsageRules_Should_OnlyTreatOperationalOutboundTypesAsUsage(
        MovementType movementType,
        bool expected)
    {
        AssetDownstreamUsageRules.OutboundMovementTypes.Contains(movementType).ShouldBe(expected);
    }
}

using FluentValidation;

namespace Application.TransferInfos.Upsert;

internal sealed class UpsertTransferInfoCommandValidator : AbstractValidator<UpsertTransferInfoCommand>
{
    public UpsertTransferInfoCommandValidator()
    {
        RuleFor(command => command.DocumentId).NotEmpty();
        RuleFor(command => command.DestinationWarehouseId).NotEmpty();
        RuleFor(command => command.TransferReason).NotEmpty().MaximumLength(200);
        RuleFor(command => command.ExpectedRowVersion).GreaterThan(0);
    }
}

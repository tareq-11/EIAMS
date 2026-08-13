using FluentValidation;

namespace Application.ReceivingInfos.Upsert;

internal sealed class UpsertReceivingInfoCommandValidator : AbstractValidator<UpsertReceivingInfoCommand>
{
    public UpsertReceivingInfoCommandValidator()
    {
        RuleFor(command => command.DocumentId).NotEmpty();
        RuleFor(command => command.SupplierRef).NotEmpty().MaximumLength(200);
        RuleFor(command => command.SupplierInvoiceRef).MaximumLength(100);
        RuleFor(command => command.ReceivingType).IsInEnum();
        RuleFor(command => command.ExpectedRowVersion).GreaterThan(0);
    }
}

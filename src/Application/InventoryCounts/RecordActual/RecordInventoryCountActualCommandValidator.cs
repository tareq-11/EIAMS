using FluentValidation;

namespace Application.InventoryCounts.RecordActual;

internal sealed class RecordInventoryCountActualCommandValidator : AbstractValidator<RecordInventoryCountActualCommand>
{
    public RecordInventoryCountActualCommandValidator()
    {
        RuleFor(command => command.CountId).NotEmpty();
        RuleFor(command => command.LineId).NotEmpty();
        RuleFor(command => command.ActualQuantity)
            .GreaterThanOrEqualTo(0)
            .Must(value => decimal.Round(value, 3) == value)
            .WithMessage("Actual quantity cannot have more than three decimal places.");
        RuleFor(command => command.ExpectedRowVersion).GreaterThan(0);
    }
}

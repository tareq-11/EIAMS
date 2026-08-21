using Application.Abstractions.Pagination;
using FluentValidation;

namespace Application.InventoryCounts.RecordActualBatch;

internal sealed class RecordInventoryCountActualsCommandValidator
    : AbstractValidator<RecordInventoryCountActualsCommand>
{
    public RecordInventoryCountActualsCommandValidator()
    {
        RuleFor(command => command.CountId).NotEmpty();
        RuleFor(command => command.ExpectedRowVersion).GreaterThan(0);
        RuleFor(command => command.Actuals)
            .NotEmpty()
            .Must(items => items.Count <= PaginationDefaults.MaximumPageSize)
            .WithMessage($"A batch cannot contain more than {PaginationDefaults.MaximumPageSize} lines.")
            .Must(items => items.Select(item => item.LineId).Distinct().Count() == items.Count)
            .WithMessage("Line ids must be unique within a batch.");
        RuleForEach(command => command.Actuals).ChildRules(item =>
        {
            item.RuleFor(value => value.LineId).NotEmpty();
            item.RuleFor(value => value.ActualQuantity)
                .GreaterThanOrEqualTo(0)
                .Must(value => decimal.Round(value, 3) == value)
                .WithMessage("Actual quantity cannot have more than three decimal places.");
        });
    }
}

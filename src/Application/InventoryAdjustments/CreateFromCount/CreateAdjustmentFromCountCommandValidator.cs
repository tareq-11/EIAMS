using FluentValidation;

namespace Application.InventoryAdjustments.CreateFromCount;

internal sealed class CreateAdjustmentFromCountCommandValidator : AbstractValidator<CreateAdjustmentFromCountCommand>
{
    public CreateAdjustmentFromCountCommandValidator() => RuleFor(command => command.CountId).NotEmpty();
}

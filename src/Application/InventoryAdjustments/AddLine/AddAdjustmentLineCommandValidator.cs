using FluentValidation;

namespace Application.InventoryAdjustments.AddLine;

internal sealed class AddAdjustmentLineCommandValidator : AbstractValidator<AddAdjustmentLineCommand>
{
    public AddAdjustmentLineCommandValidator()
    {
        RuleFor(command => command.DocumentId).NotEmpty();
        RuleFor(command => command.MaterialId).NotEmpty();
        RuleFor(command => command.Difference).NotEqual(0);
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(200);
        RuleFor(command => command.ExpectedRowVersion).GreaterThan(0);
    }
}

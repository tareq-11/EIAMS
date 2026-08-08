using FluentValidation;

namespace Application.Warehouses.SetStatus;

internal sealed class SetWarehouseStatusCommandValidator : AbstractValidator<SetWarehouseStatusCommand>
{
    public SetWarehouseStatusCommandValidator()
    {
        RuleFor(c => c.WarehouseId).NotEmpty();
        RuleFor(c => c.Status).IsInEnum();
        RuleFor(c => c.ExpectedRowVersion).GreaterThan(0);
    }
}

using FluentValidation;

namespace Application.Warehouses.Update;

internal sealed class UpdateWarehouseCommandValidator : AbstractValidator<UpdateWarehouseCommand>
{
    public UpdateWarehouseCommandValidator()
    {
        RuleFor(c => c.WarehouseId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.WarehouseType).NotEmpty().MaximumLength(50);
        RuleFor(c => c.ExpectedRowVersion).GreaterThan(0);
    }
}

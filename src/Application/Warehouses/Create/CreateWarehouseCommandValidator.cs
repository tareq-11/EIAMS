using FluentValidation;

namespace Application.Warehouses.Create;

internal sealed class CreateWarehouseCommandValidator : AbstractValidator<CreateWarehouseCommand>
{
    public CreateWarehouseCommandValidator()
    {
        RuleFor(c => c.SiteId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Code).NotEmpty().MaximumLength(50);
        RuleFor(c => c.WarehouseType).NotEmpty().MaximumLength(50);
    }
}

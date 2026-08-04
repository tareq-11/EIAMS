using FluentValidation;

namespace Application.MaterialUnitConversions.Add;

internal sealed class AddMaterialUnitConversionCommandValidator : AbstractValidator<AddMaterialUnitConversionCommand>
{
    public AddMaterialUnitConversionCommandValidator()
    {
        RuleFor(c => c.MaterialId).NotEmpty();
        RuleFor(c => c.FromUnitId).NotEmpty();
        RuleFor(c => c.ToBaseUnitId).NotEmpty();
        RuleFor(c => c.Factor).GreaterThan(0);
    }
}

using FluentValidation;

namespace Application.MaterialUnitConversions.Update;

internal sealed class UpdateMaterialUnitConversionCommandValidator
    : AbstractValidator<UpdateMaterialUnitConversionCommand>
{
    public UpdateMaterialUnitConversionCommandValidator()
    {
        RuleFor(c => c.MaterialId).NotEmpty();
        RuleFor(c => c.ConversionId).NotEmpty();
        RuleFor(c => c.Factor).GreaterThan(0);
    }
}

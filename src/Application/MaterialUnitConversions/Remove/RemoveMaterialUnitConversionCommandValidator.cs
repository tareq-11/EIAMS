using FluentValidation;

namespace Application.MaterialUnitConversions.Remove;

internal sealed class RemoveMaterialUnitConversionCommandValidator : AbstractValidator<RemoveMaterialUnitConversionCommand>
{
    public RemoveMaterialUnitConversionCommandValidator()
    {
        RuleFor(c => c.MaterialId).NotEmpty();
        RuleFor(c => c.ConversionId).NotEmpty();
    }
}

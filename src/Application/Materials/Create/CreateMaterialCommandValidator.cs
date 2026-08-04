using FluentValidation;

namespace Application.Materials.Create;

internal sealed class CreateMaterialCommandValidator : AbstractValidator<CreateMaterialCommand>
{
    public CreateMaterialCommandValidator()
    {
        RuleFor(c => c.FamilyId).NotEmpty();
        RuleFor(c => c.NameAr).NotEmpty().MaximumLength(500);
        RuleFor(c => c.NameEn).MaximumLength(500);
        RuleFor(c => c.Code).NotEmpty().MaximumLength(100);
        RuleFor(c => c.MaterialKind).IsInEnum();
        RuleFor(c => c.TrackingType).IsInEnum();
        RuleFor(c => c.Attributes)
            .Must(MaterialAttributesJson.IsValid)
            .WithMessage("Attributes must contain a valid JSON object.");
    }
}

using Domain.Materials;
using FluentValidation;

namespace Application.Materials.Create;

internal sealed class CreateMaterialCommandValidator : AbstractValidator<CreateMaterialCommand>
{
    public CreateMaterialCommandValidator()
    {
        RuleFor(c => c.FamilyId).NotEmpty();
        RuleFor(c => c.BaseUnitId).NotEmpty();
        RuleFor(c => c.NameAr).NotEmpty().MaximumLength(500);
        RuleFor(c => c.NameEn).MaximumLength(500);
        RuleFor(c => c.Code).NotEmpty().MaximumLength(100);
        RuleFor(c => c.MaterialKind).IsInEnum();
        RuleFor(c => c.TrackingType).IsInEnum();
        RuleFor(c => c.Attributes)
            .Must(MaterialAttributesJson.IsValid)
            .WithMessage("Attributes must contain a valid JSON object.");

        RuleFor(c => c.TrackingType)
            .Equal(TrackingType.Quantity)
            .When(c => c.MaterialKind == MaterialKind.Consumable)
            .WithMessage("Consumable materials must use Quantity tracking type.");

        RuleFor(c => c.TrackingType)
            .Equal(TrackingType.Serial)
            .When(c => c.MaterialKind == MaterialKind.Asset)
            .WithMessage("Asset materials must use Serial tracking type.");
    }
}

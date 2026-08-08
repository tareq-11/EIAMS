using FluentValidation;

namespace Application.WarehouseMaterialSettings.Update;

internal sealed class UpdateWarehouseMaterialSettingCommandValidator
    : AbstractValidator<UpdateWarehouseMaterialSettingCommand>
{
    public UpdateWarehouseMaterialSettingCommandValidator()
    {
        RuleFor(c => c.SettingId).NotEmpty();
        RuleFor(c => c.MinQuantity)
            .GreaterThanOrEqualTo(0)
            .PrecisionScale(18, 3, ignoreTrailingZeros: false);
        RuleFor(c => c.MaxQuantity)
            .GreaterThanOrEqualTo(0)
            .PrecisionScale(18, 3, ignoreTrailingZeros: false);
        RuleFor(c => c.MaxQuantity)
            .GreaterThanOrEqualTo(c => c.MinQuantity)
            .WithMessage("MaxQuantity must be greater than or equal to MinQuantity.");
    }
}

using FluentValidation;

namespace Application.WarehouseMaterialSettings.Create;

internal sealed class CreateWarehouseMaterialSettingCommandValidator
    : AbstractValidator<CreateWarehouseMaterialSettingCommand>
{
    public CreateWarehouseMaterialSettingCommandValidator()
    {
        RuleFor(c => c.WarehouseId).NotEmpty();
        RuleFor(c => c.MaterialId).NotEmpty();
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

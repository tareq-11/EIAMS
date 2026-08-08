using FluentValidation;

namespace Application.WarehouseMaterialSettings.SetStatus;

internal sealed class SetWarehouseMaterialSettingStatusCommandValidator
    : AbstractValidator<SetWarehouseMaterialSettingStatusCommand>
{
    public SetWarehouseMaterialSettingStatusCommandValidator()
    {
        RuleFor(c => c.SettingId).NotEmpty();
        RuleFor(c => c.Status).IsInEnum();
    }
}

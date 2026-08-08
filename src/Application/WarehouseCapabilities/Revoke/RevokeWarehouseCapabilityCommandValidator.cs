using FluentValidation;

namespace Application.WarehouseCapabilities.Revoke;

internal sealed class RevokeWarehouseCapabilityCommandValidator : AbstractValidator<RevokeWarehouseCapabilityCommand>
{
    public RevokeWarehouseCapabilityCommandValidator()
    {
        RuleFor(c => c.CapabilityId).NotEmpty();
    }
}

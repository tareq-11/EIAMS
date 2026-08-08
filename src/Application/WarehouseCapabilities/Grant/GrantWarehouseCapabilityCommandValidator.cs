using FluentValidation;

namespace Application.WarehouseCapabilities.Grant;

internal sealed class GrantWarehouseCapabilityCommandValidator : AbstractValidator<GrantWarehouseCapabilityCommand>
{
    public GrantWarehouseCapabilityCommandValidator()
    {
        RuleFor(c => c.WarehouseId).NotEmpty();
        RuleFor(c => c.MaterialDomainId).NotEmpty();
    }
}

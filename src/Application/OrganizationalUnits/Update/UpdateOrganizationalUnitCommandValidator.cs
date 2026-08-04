using FluentValidation;

namespace Application.OrganizationalUnits.Update;

internal sealed class UpdateOrganizationalUnitCommandValidator : AbstractValidator<UpdateOrganizationalUnitCommand>
{
    public UpdateOrganizationalUnitCommandValidator()
    {
        RuleFor(c => c.OrganizationalUnitId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.UnitType).NotEmpty().MaximumLength(50);
    }
}

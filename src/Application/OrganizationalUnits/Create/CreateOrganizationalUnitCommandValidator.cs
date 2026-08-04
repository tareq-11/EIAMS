using FluentValidation;

namespace Application.OrganizationalUnits.Create;

internal sealed class CreateOrganizationalUnitCommandValidator : AbstractValidator<CreateOrganizationalUnitCommand>
{
    public CreateOrganizationalUnitCommandValidator()
    {
        RuleFor(c => c.SiteId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.UnitType).NotEmpty().MaximumLength(50);
    }
}

using FluentValidation;

namespace Application.OrganizationalUnits.SetStatus;

internal sealed class SetOrganizationalUnitStatusCommandValidator : AbstractValidator<SetOrganizationalUnitStatusCommand>
{
    public SetOrganizationalUnitStatusCommandValidator()
    {
        RuleFor(c => c.OrganizationalUnitId).NotEmpty();
        RuleFor(c => c.Status).IsInEnum();
    }
}

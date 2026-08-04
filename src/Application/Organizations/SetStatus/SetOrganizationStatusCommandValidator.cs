using FluentValidation;

namespace Application.Organizations.SetStatus;

internal sealed class SetOrganizationStatusCommandValidator : AbstractValidator<SetOrganizationStatusCommand>
{
    public SetOrganizationStatusCommandValidator()
    {
        RuleFor(c => c.OrganizationId).NotEmpty();
        RuleFor(c => c.Status).IsInEnum();
    }
}

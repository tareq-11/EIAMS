using FluentValidation;

namespace Application.ExternalParties.SetStatus;

internal sealed class SetExternalPartyStatusCommandValidator : AbstractValidator<SetExternalPartyStatusCommand>
{
    public SetExternalPartyStatusCommandValidator()
    {
        RuleFor(command => command.ExternalPartyId).NotEmpty();
        RuleFor(command => command.Status).IsInEnum();
        RuleFor(command => command.ExpectedRowVersion).GreaterThan(0);
    }
}

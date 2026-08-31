using Domain.ExternalParties;
using FluentValidation;

namespace Application.ExternalParties.Create;

internal sealed class CreateExternalPartyCommandValidator : AbstractValidator<CreateExternalPartyCommand>
{
    public CreateExternalPartyCommandValidator()
    {
        RuleFor(command => command.NameAr).NotEmpty().MaximumLength(ExternalParty.MaxNameLength);
        RuleFor(command => command.Code).MaximumLength(ExternalParty.MaxCodeLength);
        RuleFor(command => command.ContactInfo).MaximumLength(ExternalParty.MaxContactInfoLength);
        RuleFor(command => command.Notes).MaximumLength(ExternalParty.MaxNotesLength);
    }
}

using Domain.ExternalParties;
using FluentValidation;

namespace Application.ExternalParties.Update;

internal sealed class UpdateExternalPartyCommandValidator : AbstractValidator<UpdateExternalPartyCommand>
{
    public UpdateExternalPartyCommandValidator()
    {
        RuleFor(command => command.ExternalPartyId).NotEmpty();
        RuleFor(command => command.NameAr).NotEmpty().MaximumLength(ExternalParty.MaxNameLength);
        RuleFor(command => command.Code).MaximumLength(ExternalParty.MaxCodeLength);
        RuleFor(command => command.ContactInfo).MaximumLength(ExternalParty.MaxContactInfoLength);
        RuleFor(command => command.Notes).MaximumLength(ExternalParty.MaxNotesLength);
        RuleFor(command => command.ExpectedRowVersion).GreaterThan(0);
    }
}

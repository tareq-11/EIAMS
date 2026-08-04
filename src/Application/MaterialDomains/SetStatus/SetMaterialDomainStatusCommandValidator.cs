using FluentValidation;

namespace Application.MaterialDomains.SetStatus;

internal sealed class SetMaterialDomainStatusCommandValidator : AbstractValidator<SetMaterialDomainStatusCommand>
{
    public SetMaterialDomainStatusCommandValidator()
    {
        RuleFor(c => c.MaterialDomainId).NotEmpty();
        RuleFor(c => c.Status).IsInEnum();
    }
}

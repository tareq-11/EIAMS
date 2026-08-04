using FluentValidation;

namespace Application.MaterialDomains.Update;

internal sealed class UpdateMaterialDomainCommandValidator : AbstractValidator<UpdateMaterialDomainCommand>
{
    public UpdateMaterialDomainCommandValidator()
    {
        RuleFor(c => c.MaterialDomainId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
    }
}

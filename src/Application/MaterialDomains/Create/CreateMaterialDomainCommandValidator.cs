using FluentValidation;

namespace Application.MaterialDomains.Create;

internal sealed class CreateMaterialDomainCommandValidator : AbstractValidator<CreateMaterialDomainCommand>
{
    public CreateMaterialDomainCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Code).NotEmpty().MaximumLength(50);
    }
}

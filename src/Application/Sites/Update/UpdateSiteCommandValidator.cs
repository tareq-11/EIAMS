using Domain.Sites;
using FluentValidation;

namespace Application.Sites.Update;

internal sealed class UpdateSiteCommandValidator : AbstractValidator<UpdateSiteCommand>
{
    public UpdateSiteCommandValidator()
    {
        RuleFor(c => c.SiteId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Location).MaximumLength(300);
        RuleFor(c => c.GovernorateCode)
            .MaximumLength(Site.MaxGovernorateCodeLength)
            .Matches("^[A-Za-z0-9_-]+$")
            .When(c => !string.IsNullOrWhiteSpace(c.GovernorateCode));
    }
}

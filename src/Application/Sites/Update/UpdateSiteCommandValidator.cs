using FluentValidation;

namespace Application.Sites.Update;

internal sealed class UpdateSiteCommandValidator : AbstractValidator<UpdateSiteCommand>
{
    public UpdateSiteCommandValidator()
    {
        RuleFor(c => c.SiteId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Location).MaximumLength(300);
    }
}

using FluentValidation;

namespace Application.Sites.SetStatus;

internal sealed class SetSiteStatusCommandValidator : AbstractValidator<SetSiteStatusCommand>
{
    public SetSiteStatusCommandValidator()
    {
        RuleFor(c => c.SiteId).NotEmpty();
        RuleFor(c => c.Status).IsInEnum();
    }
}

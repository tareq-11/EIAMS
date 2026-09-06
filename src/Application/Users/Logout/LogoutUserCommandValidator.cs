using FluentValidation;

namespace Application.Users.Logout;

internal sealed class LogoutUserCommandValidator : AbstractValidator<LogoutUserCommand>
{
    public LogoutUserCommandValidator()
    {
        RuleFor(command => command.RefreshToken)
            .MaximumLength(256)
            .When(command => !string.IsNullOrWhiteSpace(command.RefreshToken));
    }
}

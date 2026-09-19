using FluentValidation;

namespace Application.Users.Login;

internal sealed class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(command => command.Username)
            .NotEmpty()
            .MaximumLength(100)
            .Must(username => username is not null && username.All(character => character <= '\u007F'))
            .WithMessage("Username must contain ASCII characters only.");

        RuleFor(command => command.Password)
            .NotEmpty()
            .MaximumLength(128);
    }
}
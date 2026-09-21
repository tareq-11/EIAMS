using FluentValidation;
using Domain.Users;

namespace Application.Users.Login;

internal sealed class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(command => command.Username)
            .NotEmpty()
            .MaximumLength(256)
            .Must(username => username is not null && User.NormalizeUsername(username).All(character => character <= '\u007F'))
            .WithMessage("Username or email must contain ASCII characters only.");

        RuleFor(command => command.Password)
            .NotEmpty()
            .MaximumLength(128);
    }
}

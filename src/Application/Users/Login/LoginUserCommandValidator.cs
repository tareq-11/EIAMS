using Domain.Users;
using FluentValidation;

namespace Application.Users.Login;

internal sealed class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(command => command.Username)
            .NotEmpty()
            .MaximumLength(100)
            // Keep malformed credential inputs on the same generic authentication failure path;
            // the domain shape rule is enforced for persisted usernames at create/update/recovery.
            .Must(username => username is not null && User.NormalizeUsername(username).All(character => character <= '\u007F'))
            .WithMessage("Username must contain ASCII characters only.");

        RuleFor(command => command.Password)
            .NotEmpty()
            .MaximumLength(128);
    }
}

using FluentValidation;

namespace Application.Users.Login;

internal sealed class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(256)
            .EmailAddress()
            .Must(email => email is not null && email.All(character => character <= '\u007F'))
            .WithMessage("Email must contain ASCII characters only.");

        RuleFor(command => command.Password)
            .NotEmpty()
            .MaximumLength(128);
    }
}

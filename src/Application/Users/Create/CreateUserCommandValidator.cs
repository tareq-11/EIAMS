using FluentValidation;

namespace Application.Users.Create;

internal sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .Must(email => email is not null && email.All(character => character <= '\u007F'))
            .WithMessage("Email must contain ASCII characters only.")
            .MaximumLength(256);
        RuleFor(command => command.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128)
            .Matches("[A-Z]")
            .Matches("[a-z]")
            .Matches("[0-9]")
            .Matches("[^a-zA-Z0-9]");
    }
}

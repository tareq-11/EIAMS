using Domain.Users;
using FluentValidation;

namespace Application.Users.Update;

internal sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        // Positive: 0 would otherwise read as "no version supplied".
        RuleFor(command => command.ExpectedRowVersion).GreaterThan(0);
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .Must(email => email is not null && email.All(character => character <= '\u007F'))
            .WithMessage("Email must contain ASCII characters only.")
            .MaximumLength(256);
    }
}
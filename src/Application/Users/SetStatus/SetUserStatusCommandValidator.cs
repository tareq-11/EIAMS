using Domain.Users;
using FluentValidation;

namespace Application.Users.SetStatus;

internal sealed class SetUserStatusCommandValidator : AbstractValidator<SetUserStatusCommand>
{
    public SetUserStatusCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        // Positive: 0 would otherwise read as "no version supplied".
        RuleFor(command => command.ExpectedRowVersion).GreaterThan(0);
        RuleFor(command => command.Status).IsInEnum();
    }
}
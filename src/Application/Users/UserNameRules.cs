using Domain.Users;
using FluentValidation;

namespace Application.Users;

internal static class UserNameRules
{
    internal static void Apply<T>(IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder.NotEmpty()
            .MaximumLength(100)
            .Must(value => value is not null && User.IsValidUsername(User.NormalizeUsername(value)))
            .WithMessage("Username must be 3-100 ASCII letters, digits, '.', '_' or '-'.");
}

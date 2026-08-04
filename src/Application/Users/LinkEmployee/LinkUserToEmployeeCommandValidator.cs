using FluentValidation;

namespace Application.Users.LinkEmployee;

internal sealed class LinkUserToEmployeeCommandValidator : AbstractValidator<LinkUserToEmployeeCommand>
{
    public LinkUserToEmployeeCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.EmployeeId).NotEmpty();
    }
}

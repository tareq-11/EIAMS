using FluentValidation;

namespace Application.Employees.SetStatus;

internal sealed class SetEmployeeStatusCommandValidator : AbstractValidator<SetEmployeeStatusCommand>
{
    public SetEmployeeStatusCommandValidator()
    {
        RuleFor(c => c.EmployeeId).NotEmpty();
        RuleFor(c => c.Status).IsInEnum();
    }
}

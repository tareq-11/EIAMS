using FluentValidation;

namespace Application.Employees.Create;

internal sealed class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(c => c.OrgUnitId).NotEmpty();
        RuleFor(c => c.FullName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.EmployeeNumber).NotEmpty().MaximumLength(50);
        RuleFor(c => c.JobTitle).MaximumLength(100);
    }
}

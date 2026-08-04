using SharedKernel;

namespace Domain.Employees;

public static class EmployeeErrors
{
    public static Error NotFound(Guid employeeId) => Error.NotFound(
        "Employees.NotFound",
        $"The employee with the Id = '{employeeId}' was not found");

    public static readonly Error EmployeeNumberNotUnique = Error.Conflict(
        "Employees.EmployeeNumberNotUnique",
        "The provided employee number is not unique");

    public static Error OrganizationalUnitNotFound(Guid organizationalUnitId) => Error.NotFound(
        "Employees.OrganizationalUnitNotFound",
        $"The organizational unit with the Id = '{organizationalUnitId}' was not found");

    public static readonly Error Forbidden = Error.Forbidden(
        "Employees.Forbidden",
        "You are not authorized to manage employees in this site.");
}

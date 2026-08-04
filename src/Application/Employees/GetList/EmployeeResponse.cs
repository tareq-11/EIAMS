namespace Application.Employees.GetList;

public sealed class EmployeeResponse
{
    public Guid Id { get; init; }

    public Guid OrgUnitId { get; init; }

    public string FullName { get; init; }

    public string EmployeeNumber { get; init; }

    public string? JobTitle { get; init; }

    public string Status { get; init; }
}

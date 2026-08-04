using Domain.Common;
using SharedKernel;

namespace Domain.Employees;

public sealed class Employee : Entity, IAuditableEntity
{
    private Employee() { }

    public Guid OrgUnitId { get; private set; }
    public string FullName { get; private set; }
    public string EmployeeNumber { get; private set; }
    public string? JobTitle { get; private set; }
    public Status Status { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Employee Create(Guid id, Guid orgUnitId, string fullName, string employeeNumber, string? jobTitle)
    {
        var employee = new Employee
        {
            Id = id,
            OrgUnitId = orgUnitId,
            FullName = fullName,
            EmployeeNumber = employeeNumber,
            JobTitle = jobTitle,
            Status = Status.Active
        };

        employee.Raise(new EmployeeCreatedDomainEvent(employee.Id, employee.OrgUnitId));

        return employee;
    }

    public void UpdateDetails(string fullName, string? jobTitle)
    {
        FullName = fullName;
        JobTitle = jobTitle;
        Raise(new EmployeeUpdatedDomainEvent(Id));
    }

    public void SetStatus(Status status)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;

        Raise(new EmployeeStatusChangedDomainEvent(Id, status));
    }
}

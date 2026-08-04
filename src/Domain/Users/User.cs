using SharedKernel;

namespace Domain.Users;

public sealed class User : Entity, IAuditableEntity
{
    private User() { }

    public string Email { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string PasswordHash { get; private set; }
    public Guid? EmployeeId { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static User Create(Guid id, string email, string firstName, string lastName, string passwordHash)
    {
        var user = new User
        {
            Id = id,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            PasswordHash = passwordHash
        };

        user.Raise(new UserRegisteredDomainEvent(user.Id));

        return user;
    }

    public void LinkToEmployee(Guid employeeId)
    {
        EmployeeId = employeeId;

        Raise(new UserLinkedToEmployeeDomainEvent(Id, employeeId));
    }
}

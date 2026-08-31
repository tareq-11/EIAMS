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
    public UserStatus Status { get; private set; }
    public DateTime? LastLoginUtc { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static User Create(Guid id, string email, string firstName, string lastName, string passwordHash)
    {
        var user = new User
        {
            Id = id,
            Email = NormalizeEmail(email),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            PasswordHash = passwordHash,
            Status = UserStatus.Active
        };

        user.Raise(new UserRegisteredDomainEvent(user.Id));

        return user;
    }

    public void LinkToEmployee(Guid employeeId)
    {
        EmployeeId = employeeId;

        Raise(new UserLinkedToEmployeeDomainEvent(Id, employeeId));
    }

    public void UpdateProfile(string email, string firstName, string lastName)
    {
        Email = NormalizeEmail(email);
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
    }

    public void SetStatus(UserStatus status)
    {
        Status = status;
    }

    public void RecordSuccessfulLogin(DateTime occurredAtUtc)
    {
        LastLoginUtc = occurredAtUtc;
    }

    // Email canonicalization intentionally uses lower case: unlike protocol identifiers, the
    // canonical form is also returned as a human-facing address throughout the API.
#pragma warning disable CA1308
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
#pragma warning restore CA1308
}

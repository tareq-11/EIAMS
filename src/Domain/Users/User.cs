using SharedKernel;
using System.Text;

namespace Domain.Users;

public sealed class User : Entity, IAuditableEntity
{
    private User() { }

    public string Email { get; private set; }
    public string Username { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string PasswordHash { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public UserStatus Status { get; private set; }
    public DateTime? LastLoginUtc { get; private set; }

    /// <summary>
    /// Optimistic-concurrency version of the account aggregate.
    /// <para>
    /// Advances on every change to the profile or the status, so a client cannot save
    /// metadata against a version that predates a suspension (or the reverse). The user
    /// aggregate previously had no token at all, which made it the one versioned
    /// aggregate in the system where the last write silently won - the same defect the
    /// roles and role-scope assignment aggregates had already fixed.
    /// </para>
    /// </summary>
    public int RowVersion { get; private set; } = 1;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static User Create(Guid id, string email, string firstName, string lastName, string passwordHash) =>
        Create(id, email, CreateLegacyUsername(id), firstName, lastName, passwordHash);

    public static User Create(Guid id, string email, string username, string firstName, string lastName, string passwordHash)
    {
        var user = new User
        {
            Id = id,
            Email = NormalizeEmail(email),
            Username = NormalizeUsername(username),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            PasswordHash = passwordHash,
            Status = UserStatus.Active,
            RowVersion = 1
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
        UpdateProfile(email, Username, firstName, lastName);
    }

    public void UpdateProfile(string email, string username, string firstName, string lastName)
    {
        Email = NormalizeEmail(email);
        Username = NormalizeUsername(username);
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        RowVersion++;
    }

    public void SetStatus(UserStatus status)
    {
        Status = status;
        RowVersion++;
    }

    public void RecordSuccessfulLogin(DateTime occurredAtUtc)
    {
        LastLoginUtc = occurredAtUtc;
    }

    public void UpgradePasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }

    // Email canonicalization intentionally uses lower case: unlike protocol identifiers, the
    // canonical form is also returned as a human-facing address throughout the API.
#pragma warning disable CA1308
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
#pragma warning restore CA1308

    // Username canonicalization mirrors PB-002 (NFKC + trim + ToLowerInvariant). Stored lower-case
    // so callers can present either casing in the login form.
#pragma warning disable CA1308
    public static string NormalizeUsername(string username) =>
        username.Normalize(NormalizationForm.FormKC).Trim().ToLowerInvariant();
#pragma warning restore CA1308

    public static bool IsValidUsername(string username) =>
        username.Length is >= 3 and <= 100 && username.All(character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-');

    // Only used by old test fixtures and by the database migration's legacy account convention.
    // It is deterministic, unique and contains no email/local-part data.
    public static string CreateLegacyUsername(Guid id) => $"legacy-{id:N}";
}

namespace Application.Users.GetById;

public sealed record UserResponse
{
    public Guid Id { get; init; }

    public string Email { get; init; }

    public string FirstName { get; init; }

    public string LastName { get; init; }

    public Guid? EmployeeId { get; init; }

    public string? EmployeeName { get; init; }

    public string Status { get; init; }

    public DateTime? LastLoginUtc { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}

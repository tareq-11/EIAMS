namespace Application.Users.GetById;

public sealed record UserResponse
{
    public Guid Id { get; init; }

    public string Email { get; init; }

    /// <summary>
    /// The account's login identifier. Served on reads for the same reason it is
    /// served on the directory row: the UI must be able to display it.
    /// </summary>
    public string Username { get; init; }

    public string FirstName { get; init; }

    public string LastName { get; init; }

    public Guid? EmployeeId { get; init; }

    public string? EmployeeName { get; init; }

    public string Status { get; init; }

    public DateTime? LastLoginUtc { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    /// <summary>
    /// Concurrency token to submit as <c>expectedRowVersion</c> on the next write.
    /// Advances on every profile or status change.
    /// </summary>
    public int RowVersion { get; init; }
}

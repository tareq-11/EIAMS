namespace Application.Roles;

/// <summary>
/// The single authoritative role projection shared by the list read, the detail read, and the
/// metadata update result. Previously each operation declared its own identical copy, which let
/// the shapes drift; <c>RowVersion</c> in particular had to reach all three for optimistic
/// concurrency to be usable end to end.
/// </summary>
public sealed class RoleResponse
{
    public Guid Id { get; init; }

    /// <summary>The stable role code, for example <c>WH_MGR</c>.</summary>
    public string Name { get; init; }

    /// <summary>The Arabic display label the UI renders.</summary>
    public string NameAr { get; init; }

    public string? Description { get; init; }

    public IReadOnlyCollection<string> AllowedScopeTypes { get; init; } = [];

    /// <summary>
    /// The role's complete dotted permission-code set. Carried on every role read and on every write
    /// result, so a client never fans out one request per role to render a list, and never has to
    /// re-read after a successful write.
    /// </summary>
    public IReadOnlyCollection<string> PermissionCodes { get; init; } = [];

    /// <summary>
    /// Submit this value as <c>expectedRowVersion</c> on the next metadata update or permission
    /// replacement. Both operations advance the same aggregate version.
    /// </summary>
    public int RowVersion { get; init; }
}
namespace Application.Permissions.GetList;

public sealed class PermissionResponse
{
    public Guid Id { get; init; }

    public string Code { get; init; }

    /// <summary>
    /// Arabic display label. The catalogue is rendered in an Arabic UI, so this is the field a
    /// permission picker shows as the row's primary text.
    /// </summary>
    public string NameAr { get; init; }

    /// <summary>Optional Arabic explanation shown beneath the label.</summary>
    public string? DescriptionAr { get; init; }

    /// <summary>English description retained for diagnostics.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// Scope types this permission may be exercised at. A grant is only effective when it overlaps
    /// the holding role's allowed scope types, so the role administration matrix needs this to show
    /// why an otherwise valid code cannot be granted to a particular role.
    /// </summary>
    public IReadOnlyCollection<string> AllowedScopeTypes { get; init; } = [];
}
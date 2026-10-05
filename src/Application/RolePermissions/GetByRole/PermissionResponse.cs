namespace Application.RolePermissions.GetByRole;

public sealed class PermissionResponse
{
    public Guid Id { get; init; }

    public string Code { get; init; }

    /// <summary>Arabic display label, matching the catalogue projection.</summary>
    public string NameAr { get; init; }

    /// <summary>Optional Arabic explanation shown beneath the label.</summary>
    public string? DescriptionAr { get; init; }

    /// <summary>English description retained for diagnostics.</summary>
    public string? Description { get; init; }
}
namespace Application.Permissions.GetList;

public sealed class PermissionResponse
{
    public Guid Id { get; init; }

    public string Code { get; init; }

    public string? Description { get; init; }
}

namespace Application.Roles.GetById;

public sealed class RoleResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    public string? Description { get; init; }
}

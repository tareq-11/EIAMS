using SharedKernel;

namespace Domain.Roles;

/// <summary>
/// Grants a Permission to a Role. Composite key (RoleId, PermissionId) - a pure join row with no
/// independent identity, mutated directly by the assign/remove handlers.
/// </summary>
public sealed class RolePermission : IDomainEventSource
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private RolePermission() { }

    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }

    public List<IDomainEvent> DomainEvents => [.. _domainEvents];

    public static RolePermission Create(Guid roleId, Guid permissionId)
    {
        var rolePermission = new RolePermission
        {
            RoleId = roleId,
            PermissionId = permissionId
        };

        rolePermission._domainEvents.Add(new RolePermissionAssignedDomainEvent(roleId, permissionId));

        return rolePermission;
    }

    public void MarkAsRemoved()
    {
        _domainEvents.Add(new RolePermissionRemovedDomainEvent(RoleId, PermissionId));
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

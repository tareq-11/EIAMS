using SharedKernel;

namespace Domain.Roles;

public sealed record RolePermissionRemovedDomainEvent(Guid RoleId, Guid PermissionId) : IDomainEvent;

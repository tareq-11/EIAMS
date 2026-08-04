using SharedKernel;

namespace Domain.Roles;

public sealed record RolePermissionAssignedDomainEvent(Guid RoleId, Guid PermissionId) : IDomainEvent;

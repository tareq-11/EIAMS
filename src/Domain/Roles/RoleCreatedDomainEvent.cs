using SharedKernel;

namespace Domain.Roles;

public sealed record RoleCreatedDomainEvent(Guid RoleId) : IDomainEvent;

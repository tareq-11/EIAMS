using SharedKernel;

namespace Domain.Roles;

public sealed record RoleUpdatedDomainEvent(Guid RoleId) : IDomainEvent;

using SharedKernel;

namespace Domain.Users;

public sealed record UserLinkedToEmployeeDomainEvent(Guid UserId, Guid EmployeeId) : IDomainEvent;

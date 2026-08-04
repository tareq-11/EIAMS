using SharedKernel;

namespace Domain.Employees;

public sealed record EmployeeUpdatedDomainEvent(Guid EmployeeId) : IDomainEvent;

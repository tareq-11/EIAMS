using Domain.Common;
using SharedKernel;

namespace Domain.Employees;

public sealed record EmployeeStatusChangedDomainEvent(Guid EmployeeId, Status Status) : IDomainEvent;

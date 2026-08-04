using SharedKernel;

namespace Domain.UnitsOfMeasure;

public sealed record UnitOfMeasureCreatedDomainEvent(Guid UnitOfMeasureId) : IDomainEvent;

using SharedKernel;

namespace Domain.UnitsOfMeasure;

public sealed record UnitOfMeasureUpdatedDomainEvent(Guid UnitOfMeasureId) : IDomainEvent;

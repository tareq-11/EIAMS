using SharedKernel;

namespace Domain.MaterialUnitConversions;

public sealed record MaterialUnitConversionRemovedDomainEvent(Guid MaterialUnitConversionId, Guid MaterialId)
    : IDomainEvent;

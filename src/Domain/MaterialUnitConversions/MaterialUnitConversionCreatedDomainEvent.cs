using SharedKernel;

namespace Domain.MaterialUnitConversions;

public sealed record MaterialUnitConversionCreatedDomainEvent(Guid MaterialUnitConversionId, Guid MaterialId)
    : IDomainEvent;

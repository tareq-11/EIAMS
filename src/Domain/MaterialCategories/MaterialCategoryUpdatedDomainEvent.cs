using SharedKernel;

namespace Domain.MaterialCategories;

public sealed record MaterialCategoryUpdatedDomainEvent(Guid MaterialCategoryId) : IDomainEvent;

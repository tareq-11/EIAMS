using SharedKernel;

namespace Domain.MaterialCategories;

public sealed record MaterialCategoryCreatedDomainEvent(Guid MaterialCategoryId, Guid MaterialDomainId) : IDomainEvent;

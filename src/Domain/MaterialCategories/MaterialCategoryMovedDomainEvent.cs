using SharedKernel;

namespace Domain.MaterialCategories;

public sealed record MaterialCategoryMovedDomainEvent(Guid MaterialCategoryId, Guid? ParentCategoryId) : IDomainEvent;

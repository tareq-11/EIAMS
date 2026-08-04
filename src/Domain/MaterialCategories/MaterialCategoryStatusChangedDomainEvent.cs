using Domain.Common;
using SharedKernel;

namespace Domain.MaterialCategories;

public sealed record MaterialCategoryStatusChangedDomainEvent(Guid MaterialCategoryId, Status Status) : IDomainEvent;

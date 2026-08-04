using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.MaterialCategories.GetList;

public sealed record GetMaterialCategoriesQuery(
    Guid? MaterialDomainId,
    Guid? ParentCategoryId,
    bool RootOnly,
    Status? Status)
    : IQuery<List<MaterialCategoryResponse>>;

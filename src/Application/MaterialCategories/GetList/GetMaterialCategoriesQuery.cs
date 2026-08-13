using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;

namespace Application.MaterialCategories.GetList;

public sealed record GetMaterialCategoriesQuery(
    Guid? MaterialDomainId,
    Guid? ParentCategoryId,
    bool RootOnly,
    Status? Status,
    int Page,
    int PageSize)
    : IQuery<PagedResult<MaterialCategoryResponse>>;

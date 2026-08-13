using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using SharedKernel;

namespace Application.MaterialCategories.GetList;

internal sealed class GetMaterialCategoriesQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetMaterialCategoriesQuery, PagedResult<MaterialCategoryResponse>>
{
    public async Task<Result<PagedResult<MaterialCategoryResponse>>> Handle(
        GetMaterialCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        PagedResult<MaterialCategoryResponse> categories = await context.MaterialCategories
            .Where(c => query.MaterialDomainId == null || c.MaterialDomainId == query.MaterialDomainId)
            .Where(c => !query.RootOnly || c.ParentCategoryId == null)
            .Where(c => query.RootOnly || query.ParentCategoryId == null || c.ParentCategoryId == query.ParentCategoryId)
            .Where(c => query.Status == null || c.Status == query.Status)
            .Select(c => new MaterialCategoryResponse
            {
                Id = c.Id,
                MaterialDomainId = c.MaterialDomainId,
                ParentCategoryId = c.ParentCategoryId,
                Name = c.Name,
                Code = c.Code,
                Status = c.Status.ToString()
            })
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return categories;
    }
}

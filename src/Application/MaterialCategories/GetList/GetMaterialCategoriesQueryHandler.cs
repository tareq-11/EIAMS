using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.MaterialCategories.GetList;

internal sealed class GetMaterialCategoriesQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetMaterialCategoriesQuery, List<MaterialCategoryResponse>>
{
    public async Task<Result<List<MaterialCategoryResponse>>> Handle(
        GetMaterialCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        List<MaterialCategoryResponse> categories = await context.MaterialCategories
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
            .ToListAsync(cancellationToken);

        return categories;
    }
}

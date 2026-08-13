using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using SharedKernel;

namespace Application.MaterialFamilies.GetList;

internal sealed class GetMaterialFamiliesQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetMaterialFamiliesQuery, PagedResult<MaterialFamilyResponse>>
{
    public async Task<Result<PagedResult<MaterialFamilyResponse>>> Handle(
        GetMaterialFamiliesQuery query,
        CancellationToken cancellationToken)
    {
        PagedResult<MaterialFamilyResponse> families = await context.MaterialFamilies
            .Where(f => query.CategoryId == null || f.CategoryId == query.CategoryId)
            .Where(f => query.Status == null || f.Status == query.Status)
            .Select(f => new MaterialFamilyResponse
            {
                Id = f.Id,
                CategoryId = f.CategoryId,
                Name = f.Name,
                Code = f.Code,
                BaseUnitId = f.BaseUnitId,
                Status = f.Status.ToString()
            })
            .OrderBy(f => f.Name)
            .ThenBy(f => f.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return families;
    }
}

using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.MaterialFamilies.GetList;

internal sealed class GetMaterialFamiliesQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetMaterialFamiliesQuery, List<MaterialFamilyResponse>>
{
    public async Task<Result<List<MaterialFamilyResponse>>> Handle(
        GetMaterialFamiliesQuery query,
        CancellationToken cancellationToken)
    {
        List<MaterialFamilyResponse> families = await context.MaterialFamilies
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
            .ToListAsync(cancellationToken);

        return families;
    }
}

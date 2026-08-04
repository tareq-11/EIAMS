using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.MaterialFamilies;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.MaterialFamilies.GetById;

internal sealed class GetMaterialFamilyByIdQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetMaterialFamilyByIdQuery, MaterialFamilyResponse>
{
    public async Task<Result<MaterialFamilyResponse>> Handle(
        GetMaterialFamilyByIdQuery query,
        CancellationToken cancellationToken)
    {
        MaterialFamilyResponse? family = await context.MaterialFamilies
            .Where(f => f.Id == query.MaterialFamilyId)
            .Select(f => new MaterialFamilyResponse
            {
                Id = f.Id,
                CategoryId = f.CategoryId,
                Name = f.Name,
                Code = f.Code,
                BaseUnitId = f.BaseUnitId,
                Status = f.Status.ToString()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (family is null)
        {
            return Result.Failure<MaterialFamilyResponse>(MaterialFamilyErrors.NotFound(query.MaterialFamilyId));
        }

        return family;
    }
}

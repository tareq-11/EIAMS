using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.MaterialCategories;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.MaterialCategories.GetById;

internal sealed class GetMaterialCategoryByIdQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetMaterialCategoryByIdQuery, MaterialCategoryResponse>
{
    public async Task<Result<MaterialCategoryResponse>> Handle(
        GetMaterialCategoryByIdQuery query,
        CancellationToken cancellationToken)
    {
        MaterialCategoryResponse? category = await context.MaterialCategories
            .Where(c => c.Id == query.MaterialCategoryId)
            .Select(c => new MaterialCategoryResponse
            {
                Id = c.Id,
                MaterialDomainId = c.MaterialDomainId,
                ParentCategoryId = c.ParentCategoryId,
                Name = c.Name,
                Code = c.Code,
                Status = c.Status.ToString()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (category is null)
        {
            return Result.Failure<MaterialCategoryResponse>(MaterialCategoryErrors.NotFound(query.MaterialCategoryId));
        }

        return category;
    }
}

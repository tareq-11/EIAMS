using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.MaterialDomains;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.MaterialDomains.GetById;

internal sealed class GetMaterialDomainByIdQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetMaterialDomainByIdQuery, MaterialDomainResponse>
{
    public async Task<Result<MaterialDomainResponse>> Handle(
        GetMaterialDomainByIdQuery query,
        CancellationToken cancellationToken)
    {
        MaterialDomainResponse? materialDomain = await context.MaterialDomains
            .Where(d => d.Id == query.MaterialDomainId)
            .Select(d => new MaterialDomainResponse
            {
                Id = d.Id,
                Name = d.Name,
                Code = d.Code,
                Status = d.Status.ToString()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (materialDomain is null)
        {
            return Result.Failure<MaterialDomainResponse>(MaterialDomainErrors.NotFound(query.MaterialDomainId));
        }

        return materialDomain;
    }
}

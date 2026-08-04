using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.MaterialDomains.GetList;

internal sealed class GetMaterialDomainsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetMaterialDomainsQuery, List<MaterialDomainResponse>>
{
    public async Task<Result<List<MaterialDomainResponse>>> Handle(
        GetMaterialDomainsQuery query,
        CancellationToken cancellationToken)
    {
        List<MaterialDomainResponse> materialDomains = await context.MaterialDomains
            .Where(d => query.Status == null || d.Status == query.Status)
            .Select(d => new MaterialDomainResponse
            {
                Id = d.Id,
                Name = d.Name,
                Code = d.Code,
                Status = d.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return materialDomains;
    }
}

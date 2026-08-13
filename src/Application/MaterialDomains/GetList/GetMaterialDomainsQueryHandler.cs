using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using SharedKernel;

namespace Application.MaterialDomains.GetList;

internal sealed class GetMaterialDomainsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetMaterialDomainsQuery, PagedResult<MaterialDomainResponse>>
{
    public async Task<Result<PagedResult<MaterialDomainResponse>>> Handle(
        GetMaterialDomainsQuery query,
        CancellationToken cancellationToken)
    {
        PagedResult<MaterialDomainResponse> materialDomains = await context.MaterialDomains
            .Where(d => query.Status == null || d.Status == query.Status)
            .Select(d => new MaterialDomainResponse
            {
                Id = d.Id,
                Name = d.Name,
                Code = d.Code,
                Status = d.Status.ToString()
            })
            .OrderBy(d => d.Name)
            .ThenBy(d => d.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return materialDomains;
    }
}

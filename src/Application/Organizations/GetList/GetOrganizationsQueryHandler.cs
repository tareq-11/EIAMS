using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using SharedKernel;

namespace Application.Organizations.GetList;

internal sealed class GetOrganizationsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetOrganizationsQuery, PagedResult<OrganizationResponse>>
{
    public async Task<Result<PagedResult<OrganizationResponse>>> Handle(
        GetOrganizationsQuery query,
        CancellationToken cancellationToken)
    {
        PagedResult<OrganizationResponse> organizations = await context.Organizations
            .Where(o => query.Status == null || o.Status == query.Status)
            .Select(o => new OrganizationResponse
            {
                Id = o.Id,
                Name = o.Name,
                Code = o.Code,
                Status = o.Status.ToString()
            })
            .OrderBy(o => o.Name)
            .ThenBy(o => o.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return organizations;
    }
}

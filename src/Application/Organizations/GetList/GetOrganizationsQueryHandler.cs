using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Organizations.GetList;

internal sealed class GetOrganizationsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetOrganizationsQuery, List<OrganizationResponse>>
{
    public async Task<Result<List<OrganizationResponse>>> Handle(
        GetOrganizationsQuery query,
        CancellationToken cancellationToken)
    {
        List<OrganizationResponse> organizations = await context.Organizations
            .Where(o => query.Status == null || o.Status == query.Status)
            .Select(o => new OrganizationResponse
            {
                Id = o.Id,
                Name = o.Name,
                Code = o.Code,
                Status = o.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return organizations;
    }
}

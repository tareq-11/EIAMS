using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.OrganizationalUnits;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.OrganizationalUnits.GetById;

internal sealed class GetOrganizationalUnitByIdQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetOrganizationalUnitByIdQuery, OrganizationalUnitResponse>
{
    public async Task<Result<OrganizationalUnitResponse>> Handle(
        GetOrganizationalUnitByIdQuery query,
        CancellationToken cancellationToken)
    {
        OrganizationalUnitResponse? unit = await context.OrganizationalUnits
            .Where(u => u.Id == query.OrganizationalUnitId)
            .Select(u => new OrganizationalUnitResponse
            {
                Id = u.Id,
                SiteId = u.SiteId,
                ParentId = u.ParentId,
                Name = u.Name,
                UnitType = u.UnitType,
                Status = u.Status.ToString()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (unit is null)
        {
            return Result.Failure<OrganizationalUnitResponse>(
                OrganizationalUnitErrors.NotFound(query.OrganizationalUnitId));
        }

        return unit;
    }
}

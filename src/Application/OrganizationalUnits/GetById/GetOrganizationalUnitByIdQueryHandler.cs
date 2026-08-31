using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.OrganizationalUnits;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.OrganizationalUnits.GetById;

internal sealed class GetOrganizationalUnitByIdQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetOrganizationalUnitByIdQuery, OrganizationalUnitResponse>
{
    public async Task<Result<OrganizationalUnitResponse>> Handle(
        GetOrganizationalUnitByIdQuery query,
        CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionAsync(
            userContext.UserId,
            PermissionCodes.OrganizationalUnits.View,
            cancellationToken) && await scopeAuthorizationService.CanAccessOrganizationalUnitAsync(
            userContext.UserId,
            query.OrganizationalUnitId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<OrganizationalUnitResponse>(
                OrganizationalUnitErrors.NotFound(query.OrganizationalUnitId));
        }

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

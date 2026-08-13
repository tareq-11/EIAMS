using Application.Abstractions.Data;
using Application.Abstractions.Recipients;
using Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Recipients;

internal sealed class ActivePartyLookup(IApplicationDbContext context) : IActivePartyLookup
{
    public async Task<ActivePartyLookupStatus> GetStatusAsync(
        PartyType partyType,
        Guid partyId,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(partyType) || partyId == Guid.Empty)
        {
            return ActivePartyLookupStatus.UnsupportedType;
        }

        Status? status = partyType switch
        {
            PartyType.Employee => await context.Employees
                .Where(employee => employee.Id == partyId)
                .Select(employee => (Status?)employee.Status)
                .SingleOrDefaultAsync(cancellationToken),
            PartyType.OrganizationalUnit => await context.OrganizationalUnits
                .Where(organizationalUnit => organizationalUnit.Id == partyId)
                .Select(organizationalUnit => (Status?)organizationalUnit.Status)
                .SingleOrDefaultAsync(cancellationToken),
            PartyType.Site => await context.Sites
                .Where(site => site.Id == partyId)
                .Select(site => (Status?)site.Status)
                .SingleOrDefaultAsync(cancellationToken),
            PartyType.External => null,
            _ => null
        };

        if (partyType == PartyType.External)
        {
            return ActivePartyLookupStatus.UnsupportedType;
        }

        return status switch
        {
            null => ActivePartyLookupStatus.NotFound,
            Status.Active => ActivePartyLookupStatus.Active,
            _ => ActivePartyLookupStatus.Inactive
        };
    }
}

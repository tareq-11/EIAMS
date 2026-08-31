using Application.Abstractions.Data;
using Application.Abstractions.Recipients;
using Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Recipients;

internal sealed class ActivePartyLookup(ICounterpartResolver counterpartResolver) : IActivePartyLookup
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

        CounterpartResolution? counterpart = await counterpartResolver.ResolveAsync(
            partyType, partyId, cancellationToken);

        return counterpart?.Status switch
        {
            null => ActivePartyLookupStatus.NotFound,
            Status.Active => ActivePartyLookupStatus.Active,
            _ => ActivePartyLookupStatus.Inactive
        };
    }
}

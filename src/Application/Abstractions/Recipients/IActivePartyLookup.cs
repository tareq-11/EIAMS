using Domain.Common;

namespace Application.Abstractions.Recipients;

/// <summary>
/// Resolves the active state of a party selected through a polymorphic reference without exposing
/// feature-specific errors. Features translate the neutral outcome into their own error catalog.
/// </summary>
public interface IActivePartyLookup
{
    Task<ActivePartyLookupStatus> GetStatusAsync(
        PartyType partyType,
        Guid partyId,
        CancellationToken cancellationToken);
}

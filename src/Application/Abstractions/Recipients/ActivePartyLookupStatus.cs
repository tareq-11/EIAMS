namespace Application.Abstractions.Recipients;

/// <summary>The result of looking up the active state of a polymorphic operational party.</summary>
public enum ActivePartyLookupStatus
{
    Active,
    NotFound,
    Inactive,
    UnsupportedType
}

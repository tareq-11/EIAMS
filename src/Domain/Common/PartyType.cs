namespace Domain.Common;

/// <summary>
/// The kind of master record referenced by a polymorphic operational-party reference.
/// </summary>
public enum PartyType
{
    Employee,
    OrganizationalUnit,
    Site,
    External
}

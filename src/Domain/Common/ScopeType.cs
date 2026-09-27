namespace Domain.Common;

/// <summary>
/// The authorization scope a role grant (or a future capability grant) applies to (PRD Ch. 5, Ch. 10.4).
/// Enterprise is org-wide; Site covers one geographical site; OrganizationalUnit remains a
/// business-resource/custody vocabulary and is not a permitted new user-assignment scope;
/// Warehouse covers one warehouse.
/// </summary>
public enum ScopeType
{
    Enterprise,
    Site,
    OrganizationalUnit,
    Warehouse
}

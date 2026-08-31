namespace Domain.Common;

/// <summary>
/// The authorization scope a role grant (or a future capability grant) applies to (PRD Ch. 5, Ch. 10.4).
/// Enterprise is org-wide; Site covers one geographical site; OrganizationalUnit covers one
/// administrative branch and its descendants; Warehouse covers one warehouse.
/// </summary>
public enum ScopeType
{
    Enterprise,
    Site,
    OrganizationalUnit,
    Warehouse
}

namespace Domain.Common;

/// <summary>
/// The only values allowed for persisted user assignments and role-assignment contracts after 1D.
/// ScopeType remains the broader business/resource vocabulary because OrganizationalUnit is still
/// used for resource ownership, containment, and custody; it is not a user-assignment value.
/// </summary>
public enum UserAssignmentScopeType
{
    Enterprise = ScopeType.Enterprise,
    Site = ScopeType.Site,
    Warehouse = ScopeType.Warehouse
}

public static class UserAssignmentScopeTypes
{
    public static bool IsAllowed(ScopeType scopeType) => scopeType is
        ScopeType.Enterprise or ScopeType.Site or ScopeType.Warehouse;

    public static bool IsLegacyOrganizationalUnit(ScopeType scopeType) =>
        scopeType == ScopeType.OrganizationalUnit;

    public static ScopeType ToPersistedScopeType(this UserAssignmentScopeType scopeType) =>
        (ScopeType)scopeType;

    public static UserAssignmentScopeType ToAssignmentScopeType(this ScopeType scopeType) => scopeType switch
    {
        ScopeType.Enterprise => UserAssignmentScopeType.Enterprise,
        ScopeType.Site => UserAssignmentScopeType.Site,
        ScopeType.Warehouse => UserAssignmentScopeType.Warehouse,
        _ => throw new InvalidOperationException("A persisted OrganizationalUnit assignment cannot be projected as a user assignment scope.")
    };
}

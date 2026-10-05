namespace Domain.Roles;

/// <summary>
/// Fixed ids for roles seeded via migration (see Infrastructure EF configurations for the seed data
/// itself). Application code needs these directly for the secure startup bootstrap assignment,
/// so they live in Domain, which every layer can reference.
/// </summary>
public static class WellKnownRoles
{
    public static readonly Guid AdministratorId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid WarehouseKeeperId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid WarehouseManagerId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    public static readonly Guid AuditorId = Guid.Parse("00000000-0000-0000-0000-000000000004");

    /// <summary>
    /// Every role created by migration, i.e. the roles the system's own authorization model is
    /// defined in terms of.
    /// </summary>
    public static IReadOnlyCollection<Guid> SeededIds { get; } =
    [
        AdministratorId,
        WarehouseKeeperId,
        WarehouseManagerId,
        AuditorId
    ];

    /// <summary>
    /// Whether the role is one of the seeded reference roles.
    /// <para>
    /// A seeded role's <c>allowedScopeTypes</c> is part of the authorization model rather than a
    /// preference: widening it lets any caller holding <c>roles.manage</c> grant a built-in role a
    /// scope it was never meant to occupy (for example promoting Warehouse Keeper to Enterprise),
    /// which is a privilege-escalation path and not an editing convenience. The set is therefore
    /// fixed at creation for these roles and may still be widened for roles the system did not
    /// ship. Owner ruling, 2026-10-05.
    /// </para>
    /// </summary>
    public static bool IsSeeded(Guid roleId) => SeededIds.Contains(roleId);
}

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
}

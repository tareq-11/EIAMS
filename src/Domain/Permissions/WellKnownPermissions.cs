namespace Domain.Permissions;

/// <summary>
/// Fixed ids for permissions seeded via migration (see Infrastructure EF configurations for the seed
/// data itself, and <c>Application.Abstractions.Authorization.PermissionCodes</c> for the string
/// codes). Kept in Domain so any layer can reference a specific seeded permission by id if needed.
/// </summary>
public static class WellKnownPermissions
{
    public static readonly Guid UsersAccessId = Guid.Parse("00000000-0000-0000-0000-000000000101");
    public static readonly Guid OrganizationsManageId = Guid.Parse("00000000-0000-0000-0000-000000000102");
    public static readonly Guid SitesManageId = Guid.Parse("00000000-0000-0000-0000-000000000103");
    public static readonly Guid OrganizationalUnitsManageId = Guid.Parse("00000000-0000-0000-0000-000000000104");
    public static readonly Guid EmployeesManageId = Guid.Parse("00000000-0000-0000-0000-000000000105");
    public static readonly Guid RolesManageId = Guid.Parse("00000000-0000-0000-0000-000000000106");
    public static readonly Guid UnitsOfMeasureManageId = Guid.Parse("00000000-0000-0000-0000-000000000107");
    public static readonly Guid MaterialDomainsManageId = Guid.Parse("00000000-0000-0000-0000-000000000108");
    public static readonly Guid MaterialCategoriesManageId = Guid.Parse("00000000-0000-0000-0000-000000000109");
    public static readonly Guid MaterialFamiliesManageId = Guid.Parse("00000000-0000-0000-0000-000000000110");
    public static readonly Guid MaterialsManageId = Guid.Parse("00000000-0000-0000-0000-000000000111");
    public static readonly Guid WarehousesManageId = Guid.Parse("00000000-0000-0000-0000-000000000112");
    public static readonly Guid WarehouseCapabilitiesManageId = Guid.Parse("00000000-0000-0000-0000-000000000113");
    public static readonly Guid WarehouseMaterialSettingsManageId = Guid.Parse("00000000-0000-0000-0000-000000000114");
    public static readonly Guid WarehouseDocumentsViewId = Guid.Parse("00000000-0000-0000-0000-000000000115");
    public static readonly Guid WarehouseDocumentsCreateId = Guid.Parse("00000000-0000-0000-0000-000000000116");
    public static readonly Guid WarehouseDocumentsEditId = Guid.Parse("00000000-0000-0000-0000-000000000117");
    public static readonly Guid WarehouseDocumentsSubmitId = Guid.Parse("00000000-0000-0000-0000-000000000118");
    public static readonly Guid WarehouseDocumentsCancelId = Guid.Parse("00000000-0000-0000-0000-000000000119");
    public static readonly Guid WarehouseDocumentsReviewId = Guid.Parse("00000000-0000-0000-0000-000000000120");
    public static readonly Guid WarehouseDocumentsReverseId = Guid.Parse("00000000-0000-0000-0000-000000000121");
}

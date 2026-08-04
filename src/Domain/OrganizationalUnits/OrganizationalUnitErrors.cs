using SharedKernel;

namespace Domain.OrganizationalUnits;

public static class OrganizationalUnitErrors
{
    public static Error NotFound(Guid organizationalUnitId) => Error.NotFound(
        "OrganizationalUnits.NotFound",
        $"The organizational unit with the Id = '{organizationalUnitId}' was not found");

    public static Error SiteNotFound(Guid siteId) => Error.NotFound(
        "OrganizationalUnits.SiteNotFound",
        $"The site with the Id = '{siteId}' was not found");

    public static Error ParentNotFound(Guid parentId) => Error.NotFound(
        "OrganizationalUnits.ParentNotFound",
        $"The parent organizational unit with the Id = '{parentId}' was not found");

    public static Error ParentInDifferentSite(Guid parentId) => Error.Problem(
        "OrganizationalUnits.ParentInDifferentSite",
        $"The parent organizational unit with the Id = '{parentId}' belongs to a different site");

    public static readonly Error Forbidden = Error.Forbidden(
        "OrganizationalUnits.Forbidden",
        "You are not authorized to manage organizational units in this site.");
}

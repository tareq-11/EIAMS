using SharedKernel;

namespace Domain.Sites;

public static class SiteErrors
{
    public static Error NotFound(Guid siteId) => Error.NotFound(
        "Sites.NotFound",
        $"The site with the Id = '{siteId}' was not found");

    public static readonly Error CodeNotUnique = Error.Conflict(
        "Sites.CodeNotUnique",
        "The provided site code is not unique");

    public static Error OrganizationNotFound(Guid organizationId) => Error.NotFound(
        "Sites.OrganizationNotFound",
        $"The organization with the Id = '{organizationId}' was not found");

    public static readonly Error Forbidden = Error.Forbidden(
        "Sites.Forbidden",
        "You are not authorized to manage sites.");
}

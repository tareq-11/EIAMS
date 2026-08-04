using SharedKernel;

namespace Domain.Organizations;

public static class OrganizationErrors
{
    public static Error NotFound(Guid organizationId) => Error.NotFound(
        "Organizations.NotFound",
        $"The organization with the Id = '{organizationId}' was not found");

    public static readonly Error CodeNotUnique = Error.Conflict(
        "Organizations.CodeNotUnique",
        "The provided organization code is not unique");

    public static readonly Error Forbidden = Error.Forbidden(
        "Organizations.Forbidden",
        "You are not authorized to manage organizations.");
}

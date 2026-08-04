using SharedKernel;

namespace Domain.MaterialDomains;

public static class MaterialDomainErrors
{
    public static Error NotFound(Guid materialDomainId) => Error.NotFound(
        "MaterialDomains.NotFound",
        $"The material domain with the Id = '{materialDomainId}' was not found");

    public static readonly Error CodeNotUnique = Error.Conflict(
        "MaterialDomains.CodeNotUnique",
        "The provided material domain code is not unique");

    public static readonly Error Forbidden = Error.Forbidden(
        "MaterialDomains.Forbidden",
        "You are not authorized to manage material domains.");
}

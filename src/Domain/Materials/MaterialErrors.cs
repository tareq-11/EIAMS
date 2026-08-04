using SharedKernel;

namespace Domain.Materials;

public static class MaterialErrors
{
    public static Error NotFound(Guid materialId) => Error.NotFound(
        "Materials.NotFound",
        $"The material with the Id = '{materialId}' was not found");

    public static readonly Error CodeNotUnique = Error.Conflict(
        "Materials.CodeNotUnique",
        "The provided material code is not unique");

    public static Error FamilyNotFound(Guid familyId) => Error.NotFound(
        "Materials.FamilyNotFound",
        $"The material family with the Id = '{familyId}' was not found");

    public static readonly Error Forbidden = Error.Forbidden(
        "Materials.Forbidden",
        "You are not authorized to manage materials.");

    public static readonly Error ArchivedIsTerminal = Error.Conflict(
        "Materials.ArchivedIsTerminal",
        "An archived material cannot be activated or deactivated.");
}

using SharedKernel;

namespace Domain.ExternalParties;

public static class ExternalPartyErrors
{
    public static Error NotFound(Guid id) => Error.NotFound(
        "ExternalParties.NotFound",
        $"The external party with the Id = '{id}' was not found");

    public static readonly Error NameNotUnique = Error.Conflict(
        "ExternalParties.NameNotUnique",
        "An external party with the same Arabic name already exists.");

    public static readonly Error CodeNotUnique = Error.Conflict(
        "ExternalParties.CodeNotUnique",
        "An external party with the same code already exists.");

    public static readonly Error Forbidden = Error.Forbidden(
        "ExternalParties.Forbidden",
        "You are not authorized to manage external parties.");

    public static Error RowVersionMismatch(Guid id, int expected, int? current) => Error.Conflict(
        "ExternalParties.RowVersionMismatch",
        "The external party was modified by another request.",
        new { external_party_id = id, expected_row_version = expected, current_row_version = current });
}

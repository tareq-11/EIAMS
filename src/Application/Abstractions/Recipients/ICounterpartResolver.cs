using Application.Abstractions.Pagination;
using Domain.Common;
using Domain.Custodies;
using SharedKernel;
using System.Text.Json.Serialization;

namespace Application.Abstractions.Recipients;

public interface ICounterpartResolver
{
    Task<CounterpartResolution?> ResolveAsync(
        PartyType type,
        Guid id,
        CancellationToken cancellationToken);

    Task<Result<CounterpartResolution>> ValidateForWriteAsync(
        Guid userId,
        PartyType type,
        Guid id,
        CustodyKind? custodyKind,
        CancellationToken cancellationToken);

    Task<PagedResult<CounterpartResolution>> SearchActiveAsync(
        Guid userId,
        string? search,
        PartyType? type,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

public sealed record CounterpartResolution(
    [property: JsonConverter(typeof(JsonStringEnumConverter<PartyType>))] PartyType Type,
    Guid Id,
    string DisplayName,
    string? SecondaryLabelAr,
    [property: JsonConverter(typeof(JsonStringEnumConverter<Status>))] Status Status);

public static class CounterpartErrors
{
    public static readonly Error TypeInvalid = Error.Problem(
        "Counterparts.TypeInvalid", "The counterpart type is invalid.");

    public static Error NotFound(PartyType type, Guid id) => Error.NotFound(
        "Counterparts.NotFound", "The counterpart was not found.",
        new { type = type.ToString(), id });

    public static Error Inactive(PartyType type, Guid id) => Error.Problem(
        "Counterparts.Inactive", "The counterpart is inactive and cannot be used for a new operation.",
        new { type = type.ToString(), id });

    public static readonly Error OutsideScope = Error.Forbidden(
        "Counterparts.OutsideScope", "The counterpart is outside the user's effective scope.");

    public static readonly Error PersonalRequiresEmployee = Error.Problem(
        "Counterparts.PersonalRequiresEmployee", "Personal custody requires an Employee counterpart.");

    public static readonly Error OperationalRequiresNonEmployee = Error.Problem(
        "Counterparts.OperationalRequiresNonEmployee",
        "Operational custody requires an OrganizationalUnit, Site, or External counterpart.");
}

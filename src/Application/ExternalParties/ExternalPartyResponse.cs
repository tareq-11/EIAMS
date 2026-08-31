namespace Application.ExternalParties;

public sealed record ExternalPartyResponse(
    Guid Id,
    string NameAr,
    string? Code,
    string? ContactInfo,
    string? Notes,
    string Status,
    int RowVersion,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

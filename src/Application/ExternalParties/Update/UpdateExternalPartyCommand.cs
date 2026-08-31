using Application.Abstractions.Messaging;

namespace Application.ExternalParties.Update;

public sealed record UpdateExternalPartyCommand(
    Guid ExternalPartyId,
    string NameAr,
    string? Code,
    string? ContactInfo,
    string? Notes,
    int ExpectedRowVersion) : ICommand;

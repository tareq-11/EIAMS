using Application.Abstractions.Messaging;

namespace Application.ExternalParties.Create;

public sealed record CreateExternalPartyCommand(
    string NameAr,
    string? Code,
    string? ContactInfo,
    string? Notes) : ICommand<Guid>;

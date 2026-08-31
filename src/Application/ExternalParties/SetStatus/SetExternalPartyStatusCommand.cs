using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.ExternalParties.SetStatus;

public sealed record SetExternalPartyStatusCommand(
    Guid ExternalPartyId,
    Status Status,
    int ExpectedRowVersion) : ICommand;

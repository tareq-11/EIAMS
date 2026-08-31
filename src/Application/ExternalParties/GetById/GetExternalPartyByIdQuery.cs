using Application.Abstractions.Messaging;

namespace Application.ExternalParties.GetById;

public sealed record GetExternalPartyByIdQuery(Guid ExternalPartyId) : IQuery<ExternalPartyResponse>;

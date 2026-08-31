using Application.Abstractions.Messaging;
using Application.Abstractions.Recipients;
using Domain.Common;

namespace Application.Counterparts.GetById;

public sealed record GetCounterpartByIdQuery(PartyType Type, Guid CounterpartId)
    : IQuery<CounterpartResolution>;

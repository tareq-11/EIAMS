using Application.Abstractions.Messaging;

namespace Application.UnitsOfMeasure.GetById;

public sealed record GetUnitOfMeasureByIdQuery(Guid UnitOfMeasureId) : IQuery<UnitOfMeasureResponse>;

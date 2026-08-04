using Application.Abstractions.Messaging;

namespace Application.UnitsOfMeasure.GetList;

public sealed record GetUnitsOfMeasureQuery : IQuery<List<UnitOfMeasureResponse>>;

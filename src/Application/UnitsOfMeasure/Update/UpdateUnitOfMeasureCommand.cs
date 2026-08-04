using Application.Abstractions.Messaging;

namespace Application.UnitsOfMeasure.Update;

public sealed record UpdateUnitOfMeasureCommand(Guid UnitOfMeasureId, string Name, string Symbol, string UnitType)
    : ICommand;

using Application.Abstractions.Messaging;

namespace Application.UnitsOfMeasure.Create;

public sealed record CreateUnitOfMeasureCommand(string Name, string Symbol, string UnitType) : ICommand<Guid>;

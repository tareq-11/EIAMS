using Application.Abstractions.Messaging;

namespace Application.MaterialUnitConversions.Add;

public sealed record AddMaterialUnitConversionCommand(Guid MaterialId, Guid FromUnitId, Guid ToBaseUnitId, decimal Factor)
    : ICommand<Guid>;

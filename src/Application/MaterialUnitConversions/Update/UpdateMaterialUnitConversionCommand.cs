using Application.Abstractions.Messaging;

namespace Application.MaterialUnitConversions.Update;

public sealed record UpdateMaterialUnitConversionCommand(
    Guid MaterialId,
    Guid ConversionId,
    decimal Factor) : ICommand;

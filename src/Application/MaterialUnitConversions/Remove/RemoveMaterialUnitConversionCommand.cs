using Application.Abstractions.Messaging;

namespace Application.MaterialUnitConversions.Remove;

public sealed record RemoveMaterialUnitConversionCommand(Guid MaterialUnitConversionId) : ICommand;

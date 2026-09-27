using Application.Abstractions.Messaging;

namespace Application.MaterialUnitConversions.Remove;

public sealed record RemoveMaterialUnitConversionCommand(Guid MaterialId, Guid ConversionId) : ICommand;

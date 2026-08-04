using Application.Abstractions.Messaging;

namespace Application.MaterialFamilies.Update;

public sealed record UpdateMaterialFamilyCommand(Guid MaterialFamilyId, string Name, string Code) : ICommand;

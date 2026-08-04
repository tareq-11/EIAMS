using Application.Abstractions.Messaging;

namespace Application.MaterialFamilies.Create;

public sealed record CreateMaterialFamilyCommand(Guid CategoryId, string Name, string Code, Guid BaseUnitId)
    : ICommand<Guid>;

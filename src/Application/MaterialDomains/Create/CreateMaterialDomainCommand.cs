using Application.Abstractions.Messaging;

namespace Application.MaterialDomains.Create;

public sealed record CreateMaterialDomainCommand(string Name, string Code) : ICommand<Guid>;

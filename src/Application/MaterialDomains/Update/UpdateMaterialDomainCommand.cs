using Application.Abstractions.Messaging;

namespace Application.MaterialDomains.Update;

public sealed record UpdateMaterialDomainCommand(Guid MaterialDomainId, string Name) : ICommand;

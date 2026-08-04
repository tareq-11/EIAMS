using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.MaterialDomains.SetStatus;

public sealed record SetMaterialDomainStatusCommand(Guid MaterialDomainId, Status Status) : ICommand;

using Application.Abstractions.Messaging;

namespace Application.WarehouseCapabilities.Revoke;

public sealed record RevokeWarehouseCapabilityCommand(Guid CapabilityId) : ICommand;

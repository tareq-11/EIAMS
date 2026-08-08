using Application.Abstractions.Messaging;

namespace Application.WarehouseCapabilities.Grant;

public sealed record GrantWarehouseCapabilityCommand(Guid WarehouseId, Guid MaterialDomainId) : ICommand<Guid>;

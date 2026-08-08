using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.WarehouseCapabilityOperations.AddOperation;

public sealed record AddWarehouseCapabilityOperationCommand(Guid CapabilityId, OperationType OperationType)
    : ICommand<Guid>;

using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.WarehouseCapabilityOperations.RemoveOperation;

public sealed record RemoveWarehouseCapabilityOperationCommand(Guid CapabilityId, OperationType OperationType) : ICommand;

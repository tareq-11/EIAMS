using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.Warehouses.SetStatus;

public sealed record SetWarehouseStatusCommand(Guid WarehouseId, Status Status, int ExpectedRowVersion) : ICommand;

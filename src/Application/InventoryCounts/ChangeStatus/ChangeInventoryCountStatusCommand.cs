using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.InventoryCounts.ChangeStatus;

public sealed record ChangeInventoryCountStatusCommand(
    Guid CountId,
    InventoryCountStatus TargetStatus,
    int ExpectedRowVersion) : ICommand;

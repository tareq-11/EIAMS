using Application.Abstractions.Messaging;

namespace Application.InventoryCounts.SetVarianceReason;

public sealed record SetInventoryCountVarianceReasonCommand(Guid CountId, Guid LineId, string? Reason, int ExpectedRowVersion) : ICommand;

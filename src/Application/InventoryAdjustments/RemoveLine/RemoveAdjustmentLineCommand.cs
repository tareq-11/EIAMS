using Application.Abstractions.Messaging;

namespace Application.InventoryAdjustments.RemoveLine;

public sealed record RemoveAdjustmentLineCommand(
    Guid DocumentId,
    Guid LineId,
    int ExpectedRowVersion) : ICommand;

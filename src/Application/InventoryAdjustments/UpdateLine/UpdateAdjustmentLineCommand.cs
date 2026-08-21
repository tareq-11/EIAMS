using Application.Abstractions.Messaging;

namespace Application.InventoryAdjustments.UpdateLine;

public sealed record UpdateAdjustmentLineCommand(
    Guid DocumentId,
    Guid LineId,
    decimal Difference,
    Guid? UnitId,
    string Reason,
    int ExpectedRowVersion) : ICommand;

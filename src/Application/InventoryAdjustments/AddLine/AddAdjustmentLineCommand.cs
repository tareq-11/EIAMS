using Application.Abstractions.Messaging;

namespace Application.InventoryAdjustments.AddLine;

public sealed record AddAdjustmentLineCommand(
    Guid DocumentId,
    Guid MaterialId,
    decimal Difference,
    Guid? UnitId,
    string Reason,
    int ExpectedRowVersion) : ICommand<Guid>;

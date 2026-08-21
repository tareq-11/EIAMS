using Application.Abstractions.Messaging;

namespace Application.InventoryCounts.RecordActualBatch;

public sealed record InventoryCountActualInput(Guid LineId, decimal ActualQuantity);

public sealed record RecordInventoryCountActualsCommand(
    Guid CountId,
    IReadOnlyCollection<InventoryCountActualInput> Actuals,
    int ExpectedRowVersion) : ICommand;

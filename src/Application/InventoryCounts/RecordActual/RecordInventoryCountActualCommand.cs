using Application.Abstractions.Messaging;

namespace Application.InventoryCounts.RecordActual;

public sealed record RecordInventoryCountActualCommand(
    Guid CountId,
    Guid LineId,
    decimal ActualQuantity,
    int ExpectedRowVersion) : ICommand;

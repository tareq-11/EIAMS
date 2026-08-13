using Application.Abstractions.Messaging;

namespace Application.TransferInfos.Upsert;

public sealed record UpsertTransferInfoCommand(
    Guid DocumentId,
    Guid DestinationWarehouseId,
    string TransferReason,
    int ExpectedRowVersion) : ICommand;

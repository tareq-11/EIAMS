using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.ReceivingInfos.Upsert;

public sealed record UpsertReceivingInfoCommand(
    Guid DocumentId,
    string SupplierRef,
    string? SupplierInvoiceRef,
    ReceivingType ReceivingType,
    int ExpectedRowVersion) : ICommand;

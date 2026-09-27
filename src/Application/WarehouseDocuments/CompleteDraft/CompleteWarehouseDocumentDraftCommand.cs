using Application.Abstractions.Messaging;
using Application.WarehouseDocuments.GetById;
using Domain.Common;
using System.Text.Json.Serialization;

namespace Application.WarehouseDocuments.CompleteDraft;

public sealed record CompleteWarehouseDocumentDraftCommand(
    [property: JsonRequired] Guid WarehouseId,
    [property: JsonRequired] DocumentType DocumentType,
    [property: JsonRequired] IReadOnlyList<CompleteWarehouseDocumentDraftLine> Lines,
    ReceivingDraftDetails? ReceivingInfo,
    IssueDraftDetails? IssueTo,
    TransferDraftDetails? TransferInfo,
    ReturnDraftDetails? ReturnInfo,
    AdjustmentDraftDetails? AdjustmentInfo,
    [property: JsonIgnore] Guid? IdempotencyKey = null) : ICommand<WarehouseDocumentDetailsResponse>;

public sealed record CompleteWarehouseDocumentDraftLine(
    [property: JsonRequired] Guid MaterialId,
    decimal? Quantity,
    decimal? Difference,
    Guid? UnitId,
    decimal? UnitPrice,
    string? BatchNumber,
    DateOnly? ExpiryDate,
    OpeningType? OpeningType,
    IReadOnlyList<Guid>? AssetIds = null,
    string? AdjustmentReason = null);

public sealed record ReceivingDraftDetails(
    [property: JsonRequired] Guid SupplierPartyId,
    string? SupplierInvoiceRef);

public sealed record IssueDraftDetails(
    [property: JsonRequired] PartyType RecipientType,
    [property: JsonRequired] Guid RecipientId,
    [property: JsonRequired] string IssueReason);

public sealed record TransferDraftDetails(
    [property: JsonRequired] Guid DestinationWarehouseId,
    [property: JsonRequired] string TransferReason);

public sealed record ReturnDraftDetails(
    [property: JsonRequired] Guid OriginalIssueDocumentId,
    [property: JsonRequired] string ReturnReason);

public sealed record AdjustmentDraftDetails(
    [property: JsonRequired] AdjustmentKind AdjustmentKind,
    [property: JsonRequired] string Reason);

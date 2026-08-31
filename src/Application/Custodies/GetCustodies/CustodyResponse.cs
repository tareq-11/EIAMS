using System.Text.Json.Serialization;
using Domain.Common;

namespace Application.Custodies.GetCustodies;

public sealed record CustodyResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<CustodySubjectType>))]
    CustodySubjectType SubjectType,
    Guid SubjectId,
    Guid CustodyId,
    Guid MaterialId,
    string MaterialNameAr,
    string? MaterialNameEn,
    string MaterialCode,
    string MaterialKind,
    string TrackingType,
    string? SerialNumber,
    string? AssetNumber,
    Guid WarehouseId,
    string WarehouseName,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PartyType>))]
    PartyType HolderType,
    Guid HolderId,
    string HolderDisplayName,
    [property: JsonConverter(typeof(JsonStringEnumConverter<CustodyKind>))]
    CustodyKind CustodyKind,
    decimal IssuedQuantity,
    decimal ActiveQuantity,
    decimal ReturnedQuantity,
    Guid IssueDocumentId,
    string? SystemReferenceNumber,
    string Status,
    DateTime FromUtc,
    DateTime? ToUtc,
    int RowVersion);

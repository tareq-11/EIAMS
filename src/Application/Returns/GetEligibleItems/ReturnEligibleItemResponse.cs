using System.Text.Json.Serialization;
using Domain.Common;

namespace Application.Returns.GetEligibleItems;

public sealed record ReturnEligibleItemResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<CustodySubjectType>))]
    CustodySubjectType SubjectType,
    Guid SubjectId,
    Guid MaterialId,
    string MaterialNameAr,
    string? MaterialNameEn,
    string MaterialCode,
    string MaterialKind,
    string TrackingType,
    string? SerialNumber,
    string? AssetNumber,
    decimal AvailableQuantity,
    decimal IssuedQuantity);

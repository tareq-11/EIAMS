namespace Application.Reports.Documents;

public sealed record DocumentsReportRow(
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    string DocumentType,
    string DocumentStatus,
    int DocumentCount,
    DateTime? LatestCreatedAtUtc,
    DateTime? LatestPostedAtUtc);

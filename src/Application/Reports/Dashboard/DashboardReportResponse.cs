namespace Application.Reports.Dashboard;

public sealed record DashboardReportResponse(
    int WarehouseCount,
    int StockedMaterialCount,
    decimal TotalOnHandQuantity,
    int? AssetCount,
    int? ActiveCustodyCount,
    int? OpenDocumentCount,
    int? ActiveInventoryCountCount,
    DateTime GeneratedAtUtc);

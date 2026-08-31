namespace Application.Reports.CountAdjustments;

public sealed record CountAdjustmentsReportRow(
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    int CountCount,
    int ClosedCountCount,
    int VarianceLineCount,
    decimal NetVariance,
    int AdjustmentCount,
    int PostedAdjustmentCount,
    decimal AdjustmentDifference);

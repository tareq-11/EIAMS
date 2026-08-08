namespace Domain.Common;

/// <summary>
/// The type of an operational document (PRD 10.4). Owned by M2 because DocumentSequence numbering
/// needs it before WarehouseDocument (M3) exists; M3 reuses this same enum on WarehouseDocument.
/// </summary>
public enum DocumentType
{
    Receiving,
    Issue,
    Transfer,
    Adjustment,
    Opening,
    Return
}

namespace Domain.Common;

/// <summary>
/// An operation a warehouse may be granted for a material domain via WarehouseCapabilityOperation
/// (D-CAP-01, PRD 10.4).
/// </summary>
public enum OperationType
{
    Receiving,
    Issue,
    Transfer,
    Count,
    Return
}

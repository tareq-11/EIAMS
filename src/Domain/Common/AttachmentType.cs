namespace Domain.Common;

/// <summary>
/// Distinguishes the signed paper original required before posting (D-DOC-01) from any other
/// supporting attachment on a WarehouseDocument.
/// </summary>
public enum AttachmentType
{
    SignedOriginal,
    Supporting
}

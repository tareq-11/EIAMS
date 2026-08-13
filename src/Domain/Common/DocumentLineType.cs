namespace Domain.Common;

/// <summary>
/// Whether a DocumentLine tracks a plain quantity or individually numbered assets. Derived
/// server-side from the material's MaterialKind - the client cannot set this directly (PRD 10.4).
/// </summary>
public enum DocumentLineType
{
    Normal,
    Asset
}

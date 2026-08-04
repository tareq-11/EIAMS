namespace Domain.Materials;

/// <summary>Authoritative on Material only (D-CAT-01) - drives asset creation on receipt (M4+).</summary>
public enum MaterialKind
{
    Consumable,
    Durable,
    Asset
}

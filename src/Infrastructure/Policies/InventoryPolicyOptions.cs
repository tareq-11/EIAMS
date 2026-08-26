namespace Infrastructure.Policies;

/// <summary>Configuration documenting the supported v1 inventory policy.</summary>
public sealed class InventoryPolicyOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Policies:Inventory";

    /// <summary>Gets the negative-stock policy. Only <c>Block</c> is supported in v1.</summary>
    public string NegativeStockPolicy { get; init; } = Block;

    public const string Block = "Block";
}

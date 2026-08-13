namespace Application.Abstractions.Assets;

/// <summary>Safety limits for document lines and the per-unit assets created while posting.</summary>
public sealed class AssetCreationOptions
{
    public const string SectionName = "AssetCreation";

    public int MaxAssetsPerLine { get; init; } = 10_000;

    public int MaxAssetsPerDocument { get; init; } = 50_000;

    public int MaxLinesPerDocument { get; init; } = 1_000;
}

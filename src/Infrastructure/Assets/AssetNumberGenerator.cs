using Application.Abstractions.Assets;

namespace Infrastructure.Assets;

internal sealed class AssetNumberGenerator : IAssetNumberGenerator
{
    public string Generate(Guid assetId) => $"AST-{assetId:N}";
}

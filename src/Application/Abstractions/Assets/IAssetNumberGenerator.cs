namespace Application.Abstractions.Assets;

/// <summary>Generates the enterprise-unique internal number for a new Asset.</summary>
public interface IAssetNumberGenerator
{
    string Generate(Guid assetId);
}

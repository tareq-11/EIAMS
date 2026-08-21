using Application.Abstractions.Messaging;

namespace Application.Assets.GetCurrentStatus;

public sealed record GetAssetCurrentStatusQuery(Guid AssetId) : IQuery<AssetCurrentStatusResponse>;

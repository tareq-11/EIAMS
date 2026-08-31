using Application.Abstractions.Messaging;

namespace Application.Assets.GetById;

public sealed record GetAssetByIdQuery(Guid AssetId) : IQuery<AssetDetailsResponse>;

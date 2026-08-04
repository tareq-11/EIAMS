using Application.Abstractions.Messaging;

namespace Application.Materials.GetById;

public sealed record GetMaterialByIdQuery(Guid MaterialId) : IQuery<MaterialResponse>;

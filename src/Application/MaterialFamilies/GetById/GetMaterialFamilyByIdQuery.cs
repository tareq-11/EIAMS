using Application.Abstractions.Messaging;

namespace Application.MaterialFamilies.GetById;

public sealed record GetMaterialFamilyByIdQuery(Guid MaterialFamilyId) : IQuery<MaterialFamilyResponse>;

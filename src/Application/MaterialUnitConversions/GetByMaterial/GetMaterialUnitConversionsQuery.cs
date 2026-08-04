using Application.Abstractions.Messaging;

namespace Application.MaterialUnitConversions.GetByMaterial;

public sealed record GetMaterialUnitConversionsQuery(Guid MaterialId) : IQuery<List<MaterialUnitConversionResponse>>;

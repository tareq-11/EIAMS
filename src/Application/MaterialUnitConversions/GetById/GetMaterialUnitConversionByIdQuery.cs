using Application.Abstractions.Messaging;
using Application.MaterialUnitConversions.GetByMaterial;

namespace Application.MaterialUnitConversions.GetById;

public sealed record GetMaterialUnitConversionByIdQuery(
    Guid MaterialId,
    Guid ConversionId) : IQuery<MaterialUnitConversionResponse>;

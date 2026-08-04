namespace Application.MaterialUnitConversions.GetByMaterial;

public sealed class MaterialUnitConversionResponse
{
    public Guid Id { get; init; }

    public Guid MaterialId { get; init; }

    public Guid FromUnitId { get; init; }

    public Guid ToBaseUnitId { get; init; }

    public decimal Factor { get; init; }
}

namespace Application.UnitsOfMeasure.GetById;

public sealed class UnitOfMeasureResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    public string Symbol { get; init; }

    public string UnitType { get; init; }
}

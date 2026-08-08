namespace Application.WarehouseMaterialSettings.GetByWarehouse;

public sealed class WarehouseMaterialSettingResponse
{
    public Guid Id { get; init; }

    public Guid WarehouseId { get; init; }

    public Guid MaterialId { get; init; }

    public string MaterialCode { get; init; }

    public string MaterialNameAr { get; init; }

    public decimal MinQuantity { get; init; }

    public decimal MaxQuantity { get; init; }

    public string Status { get; init; }
}

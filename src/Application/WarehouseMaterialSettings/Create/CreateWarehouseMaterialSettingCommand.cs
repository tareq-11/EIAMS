using Application.Abstractions.Messaging;

namespace Application.WarehouseMaterialSettings.Create;

public sealed record CreateWarehouseMaterialSettingCommand(
    Guid WarehouseId,
    Guid MaterialId,
    decimal MinQuantity,
    decimal MaxQuantity) : ICommand<Guid>;

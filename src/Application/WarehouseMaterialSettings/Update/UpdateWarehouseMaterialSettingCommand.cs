using Application.Abstractions.Messaging;

namespace Application.WarehouseMaterialSettings.Update;

public sealed record UpdateWarehouseMaterialSettingCommand(Guid SettingId, decimal MinQuantity, decimal MaxQuantity)
    : ICommand;

using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.WarehouseMaterialSettings.SetStatus;

public sealed record SetWarehouseMaterialSettingStatusCommand(Guid SettingId, Status Status) : ICommand;

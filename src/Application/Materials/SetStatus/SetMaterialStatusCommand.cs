using Application.Abstractions.Messaging;
using Domain.Materials;

namespace Application.Materials.SetStatus;

public sealed record SetMaterialStatusCommand(Guid MaterialId, MaterialStatus Status) : ICommand;

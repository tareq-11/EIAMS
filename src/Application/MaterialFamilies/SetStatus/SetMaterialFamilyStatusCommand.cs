using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.MaterialFamilies.SetStatus;

public sealed record SetMaterialFamilyStatusCommand(Guid MaterialFamilyId, Status Status) : ICommand;

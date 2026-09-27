using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.UnitsOfMeasure.SetStatus;

public sealed record SetUnitOfMeasureStatusCommand(Guid UnitOfMeasureId, Status Status) : ICommand;

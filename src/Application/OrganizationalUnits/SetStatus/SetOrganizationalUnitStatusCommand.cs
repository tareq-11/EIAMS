using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.OrganizationalUnits.SetStatus;

public sealed record SetOrganizationalUnitStatusCommand(Guid OrganizationalUnitId, Status Status) : ICommand;

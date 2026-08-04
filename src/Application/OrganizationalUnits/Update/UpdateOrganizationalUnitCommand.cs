using Application.Abstractions.Messaging;

namespace Application.OrganizationalUnits.Update;

public sealed record UpdateOrganizationalUnitCommand(Guid OrganizationalUnitId, string Name, string UnitType) : ICommand;

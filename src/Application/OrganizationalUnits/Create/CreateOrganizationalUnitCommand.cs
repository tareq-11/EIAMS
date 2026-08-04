using Application.Abstractions.Messaging;

namespace Application.OrganizationalUnits.Create;

public sealed record CreateOrganizationalUnitCommand(Guid SiteId, Guid? ParentId, string Name, string UnitType)
    : ICommand<Guid>;

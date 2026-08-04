using Application.Abstractions.Messaging;

namespace Application.OrganizationalUnits.GetById;

public sealed record GetOrganizationalUnitByIdQuery(Guid OrganizationalUnitId) : IQuery<OrganizationalUnitResponse>;

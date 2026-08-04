using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.OrganizationalUnits.GetList;

public sealed record GetOrganizationalUnitsQuery(Guid? SiteId, Guid? ParentId, Status? Status)
    : IQuery<List<OrganizationalUnitResponse>>;

using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.Organizations.GetList;

public sealed record GetOrganizationsQuery(Status? Status) : IQuery<List<OrganizationResponse>>;

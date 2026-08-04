using Application.Abstractions.Messaging;

namespace Application.Organizations.GetById;

public sealed record GetOrganizationByIdQuery(Guid OrganizationId) : IQuery<OrganizationResponse>;

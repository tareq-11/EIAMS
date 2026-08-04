using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.Sites.GetList;

public sealed record GetSitesQuery(Guid? OrganizationId, Status? Status) : IQuery<List<SiteResponse>>;

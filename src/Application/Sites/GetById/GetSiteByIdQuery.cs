using Application.Abstractions.Messaging;

namespace Application.Sites.GetById;

public sealed record GetSiteByIdQuery(Guid SiteId) : IQuery<SiteResponse>;

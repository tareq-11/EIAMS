using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.MaterialDomains.GetList;

public sealed record GetMaterialDomainsQuery(Status? Status) : IQuery<List<MaterialDomainResponse>>;

using Application.Abstractions.Messaging;

namespace Application.MaterialDomains.GetById;

public sealed record GetMaterialDomainByIdQuery(Guid MaterialDomainId) : IQuery<MaterialDomainResponse>;

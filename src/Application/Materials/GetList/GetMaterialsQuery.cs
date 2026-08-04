using Application.Abstractions.Messaging;
using Domain.Materials;

namespace Application.Materials.GetList;

public sealed record GetMaterialsQuery(Guid? FamilyId, Guid? MaterialDomainId, MaterialStatus? Status)
    : IQuery<List<MaterialResponse>>;

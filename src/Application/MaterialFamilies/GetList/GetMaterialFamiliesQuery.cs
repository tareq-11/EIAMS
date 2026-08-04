using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.MaterialFamilies.GetList;

public sealed record GetMaterialFamiliesQuery(Guid? CategoryId, Status? Status) : IQuery<List<MaterialFamilyResponse>>;
